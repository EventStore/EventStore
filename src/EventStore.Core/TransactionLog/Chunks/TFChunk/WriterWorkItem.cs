// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DotNext;
using DotNext.IO;
using EventStore.Plugins.Transforms;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk;

internal sealed class WriterWorkItem : Disposable {
	public const int BufferSize = 8192;

	public Stream WorkingStream { get; private set; }

	private readonly Stream _fileStream;
	private Stream _memStream;
	public readonly IncrementalHash MD5;

	public unsafe WriterWorkItem(nint memoryPtr, int length, IncrementalHash md5,
		IChunkWriteTransform chunkWriteTransform, int initialStreamPosition) {
		var memStream = new UnmanagedMemoryStream((byte*)memoryPtr, length, length, FileAccess.ReadWrite) {
			Position = initialStreamPosition,
		};

		var chunkDataWriteStream = new ChunkDataWriteStream(memStream, md5);
		WorkingStream = _memStream = chunkWriteTransform.TransformData(chunkDataWriteStream);
		MD5 = md5;
	}

	public WriterWorkItem(IChunkHandle handle, IncrementalHash md5, bool unbuffered,
		IChunkWriteTransform chunkWriteTransform, int initialStreamPosition) {
		var chunkStream = handle.CreateStream();
		var fileStream = unbuffered
			? chunkStream
			: new PoolingBufferedStream(chunkStream) { MaxBufferSize = BufferSize };
		fileStream.Position = initialStreamPosition;
		var chunkDataWriteStream = new ChunkDataWriteStream(fileStream, md5);

		WorkingStream = _fileStream = chunkWriteTransform.TransformData(chunkDataWriteStream);
		MD5 = md5;
	}

	public void SetMemStream(UnmanagedMemoryStream memStream) {
		_memStream = memStream;
		if (_fileStream is null)
			WorkingStream = memStream;
	}

	public ValueTask AppendData(ReadOnlyMemory<byte> buf, CancellationToken token) {
		// MEMORY (in-memory write doesn't require async I/O)
		_memStream?.Write(buf.Span);

		// as we are always append-only, stream's position should be right here
		return _fileStream?.WriteAsync(buf, token) ?? ValueTask.CompletedTask;
	}

	public void ResizeStream(long fileSize) {
		_fileStream?.SetLength(fileSize);
		_memStream?.SetLength(fileSize);
	}

	protected override void Dispose(bool disposing) {
		if (disposing) {
			_fileStream?.Dispose();
			DisposeMemStream();
			MD5.Dispose();
		}

		base.Dispose(disposing);
	}

	public ValueTask FlushToDisk(CancellationToken token) {
		// in-mem stream doesn't require async call
		_memStream?.Flush();

		return new(_fileStream is not null ? _fileStream.FlushAsync(token) : Task.CompletedTask);
	}

	public void FlushToDisk() {
		_fileStream?.Flush();
		_memStream?.Flush();
	}

	public void DisposeMemStream() {
		_memStream?.Dispose();
		_memStream = null;
	}
}
