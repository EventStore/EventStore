﻿using System.Threading;

namespace EventStore.Core.TransactionLog.Scavenging {
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
		void Accumulate(
			ScavengePoint prevScavengePoint,
			ScavengePoint scavengePoint,
			IScavengeStateForAccumulator<TStreamId> state,
			CancellationToken cancellationToken);

		void Accumulate(
			ScavengeCheckpoint.Accumulating checkpoint,
			IScavengeStateForAccumulator<TStreamId> state,
			CancellationToken cancellationToken);
	}
}
