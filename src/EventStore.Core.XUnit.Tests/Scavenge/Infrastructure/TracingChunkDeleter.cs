// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Scavenging;

namespace EventStore.Core.XUnit.Tests.Scavenge;

public class TracingChunkDeleter<TStreamId, TRecord> :
	IChunkDeleter<TStreamId, TRecord> {

	private readonly IChunkDeleter<TStreamId, TRecord> _wrapped;
	private readonly Tracer _tracer;

	public TracingChunkDeleter(IChunkDeleter<TStreamId, TRecord> wrapped, Tracer tracer) {
		_wrapped = wrapped;
		_tracer = tracer;
	}

	public async ValueTask<bool> DeleteIfNotRetained(
		ScavengePoint scavengePoint,
		IScavengeStateForChunkExecutorWorker<TStreamId> concurrentState,
		IChunkReaderForExecutor<TStreamId, TRecord> physicalChunk,
		CancellationToken ct) {

		var deleted = await _wrapped.DeleteIfNotRetained(scavengePoint, concurrentState, physicalChunk, ct);
		var decision = deleted ? "Deleted" : "Retained";
		_tracer.Trace($"{decision} Chunk {physicalChunk.ChunkStartNumber}-{physicalChunk.ChunkEndNumber}");
		return deleted;
	}
}
