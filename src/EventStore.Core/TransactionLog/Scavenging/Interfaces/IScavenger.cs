// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.TransactionLog.Scavenging.Interfaces;

// There are two kinds of streams that we might want to remove events from
//    - original streams (i.e. streams with metadata)
//        - according to tombstone
//        - according to metadata (maxage, maxcount, tb)
//    - metadata streams
//        - according to tombstone
//        - maxcount 1
//
// In a nutshell:
// - The Accumulator passes through the log once (in total, not per scavenge)
//   accumulating state that we need and calculating some DiscardPoints.
// - When a ScavengePoint is set, the Calculator uses it to finish calculating
//   the DiscardPoints.
// - The Chunk and Index Executors can then use this information to perform the
//   actual record/indexEntry removal.
// - Merger merges the chunks
// - Cleaner removes parts of the scavenge state that are no longer needed
//   (as determined by the calculator)
public interface IScavenger : IDisposable {
	string ScavengeId { get; }
	Task<ScavengeResult> ScavengeAsync(CancellationToken cancellationToken);
}
