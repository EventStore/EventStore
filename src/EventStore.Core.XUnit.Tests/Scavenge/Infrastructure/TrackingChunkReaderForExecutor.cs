// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Threading;
using EventStore.Core.TransactionLog.Scavenging;

namespace EventStore.Core.XUnit.Tests.Scavenge;

public class TrackingChunkReaderForExecutor<TStreamId, TRecord> :
	IChunkReaderForExecutor<TStreamId, TRecord> {

	private readonly IChunkReaderForExecutor<TStreamId, TRecord> _wrapped;
	private readonly bool _isRemote;
	private readonly Tracer _tracer;

	public TrackingChunkReaderForExecutor(
		IChunkReaderForExecutor<TStreamId, TRecord> wrapped,
		bool isRemote,
		Tracer tracer) {

		_wrapped = wrapped;
		_isRemote = isRemote;
		_tracer = tracer;
	}

	public string Name => _wrapped.Name;

	public int FileSize => _wrapped.FileSize;

	public int ChunkStartNumber => _wrapped.ChunkStartNumber;

	public int ChunkEndNumber => _wrapped.ChunkEndNumber;

	public bool IsReadOnly => _wrapped.IsReadOnly;

	public bool IsRemote => _isRemote;

	public long ChunkStartPosition => _wrapped.ChunkStartPosition;

	public long ChunkEndPosition => _wrapped.ChunkEndPosition;

	public IAsyncEnumerable<bool> ReadInto(
		RecordForExecutor<TStreamId, TRecord>.NonPrepare nonPrepare,
		RecordForExecutor<TStreamId, TRecord>.Prepare prepare,
		CancellationToken token) {

		_tracer.Trace($"Opening Chunk {ChunkStartNumber}-{ChunkEndNumber}");
		return _wrapped.ReadInto(nonPrepare, prepare, token);
	}
}
