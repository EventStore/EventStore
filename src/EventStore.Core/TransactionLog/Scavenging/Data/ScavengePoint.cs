// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class ScavengePoint {
		public ScavengePoint(long position, long eventNumber, DateTime effectiveNow, int threshold) {
			Position = position;
			EventNumber = eventNumber;
			EffectiveNow = effectiveNow;
			Threshold = threshold;
		}

		// the position to scavenge up to (exclusive)
		public long Position { get; }

		public long EventNumber { get; }

		public DateTime EffectiveNow { get; }

		// The minimum a physical chunk must weigh before we will execute it.
		// Stored in the scavenge point so that (later) we could specify the threshold when
		// running the scavenge, and have it affect that scavenge on all of the nodes.
		public int Threshold { get; }

		public string GetName() => $"SP-{EventNumber}";

		public override string ToString() =>
			$"{GetName()}. Position: {Position:N0}, EffectiveNow: {EffectiveNow}, Threshold: {Threshold}.";
	}
}
