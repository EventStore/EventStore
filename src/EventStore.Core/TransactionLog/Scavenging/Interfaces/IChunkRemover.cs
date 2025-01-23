// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.TransactionLog.Scavenging.Interfaces;

// responsible for removing chunks that are present in the archive and no longer
// needed locally according to the retention policy
public interface IChunkRemover<TStreamId, TRecord> {
	static IChunkRemover<TStreamId, TRecord> NoOp => NoOpChunkRemover<TStreamId, TRecord>.Instance;

	// returns true iff removing
	ValueTask<bool> StartRemovingIfNotRetained(
		ScavengePoint scavengePoint,
		IScavengeStateForChunkExecutorWorker<TStreamId> concurrentState,
		IChunkReaderForExecutor<TStreamId, TRecord> physicalChunk,
		CancellationToken ct);
}

file class NoOpChunkRemover<TStreamId, TRecord> : IChunkRemover<TStreamId, TRecord> {
	public static NoOpChunkRemover<TStreamId, TRecord> Instance { get; } = new();

	public ValueTask<bool> StartRemovingIfNotRetained(
		ScavengePoint scavengePoint,
		IScavengeStateForChunkExecutorWorker<TStreamId> concurrentState,
		IChunkReaderForExecutor<TStreamId, TRecord> physicalChunk, CancellationToken ct) {

		return new(false);
	}
}
