// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Scavenging;
using EventStore.Core.TransactionLog.Scavenging.Interfaces;

namespace EventStore.Core.XUnit.Tests.Scavenge.Infrastructure;

public class TracingChunkRemover<TStreamId, TRecord> :
	IChunkRemover<TStreamId, TRecord> {

	private readonly IChunkRemover<TStreamId, TRecord> _wrapped;
	private readonly Tracer _tracer;

	public TracingChunkRemover(IChunkRemover<TStreamId, TRecord> wrapped, Tracer tracer) {
		_wrapped = wrapped;
		_tracer = tracer;
	}

	public async ValueTask<bool> StartRemovingIfNotRetained(
		ScavengePoint scavengePoint,
		IScavengeStateForChunkExecutorWorker<TStreamId> concurrentState,
		IChunkReaderForExecutor<TStreamId, TRecord> physicalChunk,
		CancellationToken ct) {

		var removing = await _wrapped.StartRemovingIfNotRetained(scavengePoint, concurrentState, physicalChunk, ct);
		var decision = removing ? "Removing" : "Retaining";
		_tracer.Trace($"{decision} Chunk {physicalChunk.ChunkStartNumber}-{physicalChunk.ChunkEndNumber}");
		return removing;
	}
}