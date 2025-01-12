// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Diagnostics.Metrics;
using EventStore.Core.Time;

namespace EventStore.Core.Metrics;

public static class TempIndexMetrics {
	static DurationMetric IndexDuration;
	static DurationMetric ReadDuration;

	public static void Setup(Meter meter) {
		IndexDuration = new(meter, "eventstore-temp-index-duration");
		ReadDuration = new(meter, "eventstore-temp-read-duration");
	}

	public static Duration MeasureIndex(string name) => new(IndexDuration, name, Instant.Now);

	public static Duration MeasureRead(string name) => new(ReadDuration, name, Instant.Now);
}
