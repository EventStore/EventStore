// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using DotNext.IO.Pipelines;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Chunks.TFChunk;

namespace EventStore.Core.TransactionLog.Chunks;

public abstract class TFChunkBulkReader : IChunkBlobReader {
	public TFChunk.TFChunk Chunk {
		get { return _chunk; }
	}

	internal Stream Stream {
		get { return _stream; }
	}

	private readonly TFChunk.TFChunk _chunk;
	private readonly Stream _stream;
	private bool _disposed;
	public bool IsMemory { get; }

	protected TFChunkBulkReader(TFChunk.TFChunk chunk, Stream streamToUse, bool isMemory) {
		Ensure.NotNull(chunk, "chunk");
		Ensure.NotNull(streamToUse, "stream");
		_chunk = chunk;
		_stream = streamToUse;
		IsMemory = isMemory;
	}

	public abstract void SetPosition(long position);
	public abstract ValueTask<BulkReadResult> ReadNextBytes(Memory<byte> buffer, CancellationToken token);

	~TFChunkBulkReader() {
		Dispose();
	}

	public void Release() {
		_stream.Dispose();
		_disposed = true;
		_chunk.ReleaseReader(this);
	}

	public void Dispose() {
		if (_disposed)
			return;
		Release();
		GC.SuppressFinalize(this);
	}
}
