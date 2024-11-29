// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotNext;
using DotNext.IO;
using EventStore.Plugins.Transforms;
using Microsoft.IO;
using Microsoft.Win32.SafeHandles;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk;

internal sealed class WriterWorkItem : Disposable {
	public const int BufferSize = 8192;

	public Stream WorkingStream { get; private set; }

	private readonly Stream _fileStream;
	private Stream _memStream;

	public readonly BinaryWriter BufferWriter;
	public readonly HashAlgorithm MD5;

	public unsafe WriterWorkItem(nint memoryPtr, int length, HashAlgorithm md5,
		IChunkWriteTransform chunkWriteTransform, int initialStreamPosition) {
		var memStream = new UnmanagedMemoryStream((byte*)memoryPtr, length, length, FileAccess.ReadWrite) {
			Position = initialStreamPosition,
		};

		var chunkDataWriteStream = new ChunkDataWriteStream(memStream, md5);
		WorkingStream = _memStream = chunkWriteTransform.TransformData(chunkDataWriteStream);
		BufferWriter = new(new MemoryStream(BufferSize), Encoding.UTF8, leaveOpen: false);
		MD5 = md5;
	}

	public WriterWorkItem(SafeFileHandle handle, HashAlgorithm md5, bool unbuffered,
		IChunkWriteTransform chunkWriteTransform, int initialStreamPosition) {
		var fileStream = unbuffered
			? handle.AsUnbufferedStream(FileAccess.ReadWrite)
			: new BufferedStream(handle.AsUnbufferedStream(FileAccess.ReadWrite), BufferSize);
		fileStream.Position = initialStreamPosition;
		var chunkDataWriteStream = new ChunkDataWriteStream(fileStream, md5);

		WorkingStream = _fileStream = chunkWriteTransform.TransformData(chunkDataWriteStream);
		BufferWriter = new(new MemoryStream(BufferSize), Encoding.UTF8, leaveOpen: false);
		MD5 = md5;
	}

	public ReadOnlyMemory<byte> WrittenBuffer {
		get {
			Debug.Assert(BufferWriter.BaseStream is MemoryStream);

			var stream = Unsafe.As<MemoryStream>(BufferWriter.BaseStream);
			return new(stream.GetBuffer(), 0, (int)stream.Length);
		}
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

	public void ResizeStream(int fileSize) {
		_fileStream?.SetLength(fileSize);
		_memStream?.SetLength(fileSize);
	}

	protected override void Dispose(bool disposing) {
		if (disposing) {
			_fileStream?.Dispose();
			DisposeMemStream();
			BufferWriter.Dispose();
			MD5.Dispose();
		}

		base.Dispose(disposing);
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
