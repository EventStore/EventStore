// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.TransactionLog.Scavenging.Interfaces;

// responsible for deleting chunks that are present in the archive and no longer
// needed locally according to the retention policy
public interface IChunkDeleter<TStreamId, TRecord> {
	static IChunkDeleter<TStreamId, TRecord> NoOp => NoOpChunkDeleter<TStreamId, TRecord>.Instance;

	// returns true iff deleted
	ValueTask<bool> DeleteIfNotRetained(
		ScavengePoint scavengePoint,
		IScavengeStateForChunkExecutorWorker<TStreamId> concurrentState,
		IChunkReaderForExecutor<TStreamId, TRecord> physicalChunk,
		CancellationToken ct);
}

file class NoOpChunkDeleter<TStreamId, TRecord> : IChunkDeleter<TStreamId, TRecord> {
	public static NoOpChunkDeleter<TStreamId, TRecord> Instance { get; } = new();

	public ValueTask<bool> DeleteIfNotRetained(
		ScavengePoint scavengePoint,
		IScavengeStateForChunkExecutorWorker<TStreamId> concurrentState,
		IChunkReaderForExecutor<TStreamId, TRecord> physicalChunk, CancellationToken ct) {

		return new(false);
	}
}
