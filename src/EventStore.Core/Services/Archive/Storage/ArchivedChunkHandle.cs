// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNext.IO;
using EventStore.Core.TransactionLog.Chunks.TFChunk;

namespace EventStore.Core.Services.Archive.Storage;

internal sealed class ArchivedChunkHandle : IChunkHandle {
	private readonly IArchiveStorageReader _reader;
	private readonly int _logicalChunkNumber;
	private readonly long _length;

	private ArchivedChunkHandle(IArchiveStorageReader reader, int logicalChunkNumber, long length) {
		Debug.Assert(reader is not null);
		Debug.Assert(length >= 0L);

		_reader = reader;
		_logicalChunkNumber = logicalChunkNumber;
		_length = length;
	}

	public static async ValueTask<IChunkHandle> OpenForReadAsync(IArchiveStorageReader reader, int logicalChunkNumber, CancellationToken token) {
		var metadata = await reader.GetMetadataAsync(logicalChunkNumber, token);
		return new ArchivedChunkHandle(reader, logicalChunkNumber, metadata.Size);
	}

	void IFlushable.Flush() {
		// nothing to flush
	}

	Task IFlushable.FlushAsync(CancellationToken token) => CompletedOrCanceled(token);

	ValueTask IChunkHandle.WriteAsync(ReadOnlyMemory<byte> data, long offset, CancellationToken token)
		=> ValueTask.FromException(new NotSupportedException());

	public ValueTask<int> ReadAsync(Memory<byte> buffer, long offset, CancellationToken token)
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
