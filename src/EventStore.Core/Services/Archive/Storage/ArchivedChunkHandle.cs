// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNext.IO;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using EventStore.Core.TransactionLog.Chunks.TFChunk;

namespace EventStore.Core.Services.Archive.Storage;

// A handle to a specific version of a specific chunk.
// This detects if the remote chunk has been replaced with a new version.
// If instead we did this check at a higher level (the UnbufferedStream that uses the IChunkHandle)
// then it would be doing checks even for IChunkHandles where it is impossible for the version to change.
internal sealed class ArchivedChunkHandle : IChunkHandle {
	private readonly IArchiveStorageReader _reader;
	private readonly int _logicalChunkNumber;
	private readonly long _length;
	private readonly string _etag;

	private ArchivedChunkHandle(IArchiveStorageReader reader, int logicalChunkNumber, long length,
		string etag) {

		Debug.Assert(reader is not null);
		Debug.Assert(length >= 0L);

		_reader = reader;
		_logicalChunkNumber = logicalChunkNumber;
		_length = length;
		_etag = etag;
	}

	public static async ValueTask<IChunkHandle> OpenForReadAsync(IArchiveStorageReader reader, int logicalChunkNumber, CancellationToken token) {
		var metadata = await reader.GetMetadataAsync(logicalChunkNumber, token);
		return new ArchivedChunkHandle(reader, logicalChunkNumber, metadata.PhysicalSize, metadata.ETag);
	}

	void IFlushable.Flush() {
		// nothing to flush
	}

	Task IFlushable.FlushAsync(CancellationToken token) => CompletedOrCanceled(token);

	ValueTask IChunkHandle.WriteAsync(ReadOnlyMemory<byte> data, long offset, CancellationToken token)
		=> ValueTask.FromException(new NotSupportedException());

	public async ValueTask<int> ReadAsync(Memory<byte> buffer, long offset, CancellationToken token) {
		var (readCount, etag) = await _reader.ReadAsync(_logicalChunkNumber, buffer, offset, token);

		if (etag != _etag)
			throw new WrongETagException(
				objectName: $"Chunk {_logicalChunkNumber}",
				expected: _etag,
				actual: etag);

		return readCount;
	}

	// does it make sense to have the places that want to read this handle receive the etag?
	public ValueTask<(int, string)> ReadAsync2(Memory<byte> buffer, long offset, CancellationToken token)
		=> _reader.ReadAsync(_logicalChunkNumber, buffer, offset, token);

	public long Length {
		get => _length;
		set => throw new NotSupportedException();
	}

	public FileAccess Access { get; init; } = FileAccess.Read;

	private static Task CompletedOrCanceled(CancellationToken token)
		=> token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;

	void IDisposable.Dispose() {
		// nothing to dispose
	}
}
