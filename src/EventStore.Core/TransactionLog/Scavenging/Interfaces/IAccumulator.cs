// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.TransactionLog.Scavenging.Interfaces;

// The Accumulator reads through the log up to the scavenge point
// its purpose is to do any log scanning that is necessary for a scavenge _only once_
// accumulating whatever state is necessary to avoid subsequent scans.
//
// in practice it populates the scavenge state with:
//  1. the scavengable streams
//  2. hash collisions between any streams
//  3. most recent metadata and whether the stream is tombstoned
//  4. discard points for metadata streams
//  5. data for maxage calculations - maybe that can be another IScavengeMap
public interface IAccumulator<TStreamId> {
	ValueTask Accumulate(
		ScavengePoint prevScavengePoint,
		ScavengePoint scavengePoint,
		IScavengeStateForAccumulator<TStreamId> state,
		CancellationToken cancellationToken);

	ValueTask Accumulate(
		ScavengeCheckpoint.Accumulating checkpoint,
		IScavengeStateForAccumulator<TStreamId> state,
		CancellationToken cancellationToken);
}
