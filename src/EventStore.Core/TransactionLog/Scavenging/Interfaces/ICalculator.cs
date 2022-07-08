﻿using System.Threading;

namespace EventStore.Core.TransactionLog.Scavenging {
	// The Calculator calculates the DiscardPoints that depend on the ScavengePoint
	// (after which all the scavengable streams will have discard points calculated correctly)
	//
	// It also creates a heuristic for which chunks are most in need of scavenging.
	//
	// The job of calculating the DiscardPoints is split between the Accumulator and the Calculator.
	// The Accumulator calculates them for Metadata streams because (a) it is simple and
	// (b) it is updating the state for each metastream record anyway.
	// The Calculator calculates them for Original streams so save us updating them repeatedly as
	// we accumulate each event.
	//
	// For streams that do not collide (which is ~all of them) the calculation can be done index-only.
	// that is, without hitting the log at all.
	public interface ICalculator<TStreamId> {
		void Calculate(
			ScavengePoint scavengePoint,
			IScavengeStateForCalculator<TStreamId> source,
			CancellationToken cancellationToken);

		void Calculate(
			ScavengeCheckpoint.Calculating<TStreamId> checkpoint,
			IScavengeStateForCalculator<TStreamId> source,
			CancellationToken cancellationToken);
	}
}
