// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.TransactionLog.Scavenging;

// store a range per chunk so that the calculator can definitely get a timestamp range for each event
// that is guaranteed to contain the real timestamp of that event.
public struct ChunkTimeStampRange : IEquatable<ChunkTimeStampRange> {
	public ChunkTimeStampRange(DateTime min, DateTime max) {
		Min = min;
		Max = max;
	}

	public DateTime Min { get; }

	public DateTime Max { get; }

	public bool Equals(ChunkTimeStampRange other) =>
		Min == other.Min &&
		Max == other.Max;

	// avoid the default, reflection based, implementations if we ever need to call these
	public override int GetHashCode() => throw new NotImplementedException();
	public override bool Equals(object other) => throw new NotImplementedException();
}
