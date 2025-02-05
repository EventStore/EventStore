// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Diagnostics.Metrics;
using EventStore.Core.Time;

namespace EventStore.Core.Metrics;

public class DurationMetric {
	private readonly Histogram<double> _histogram;
	private readonly IClock _clock;

	public DurationMetric(Meter meter, string name, IClock clock = null) {
		_clock = clock ?? Clock.Instance;
		_histogram = meter.CreateHistogram<double>(name, "seconds");
	}

	public Duration Start(string durationName) =>
		new(this, durationName, _clock.Now);

	public Instant Record(
		Instant start,
		KeyValuePair<string, object> tag1,
		KeyValuePair<string, object> tag2) {

		var now = _clock.Now;
		var elapsedSeconds = now.ElapsedSecondsSince(start);
		_histogram.Record(elapsedSeconds, tag1, tag2);
		return now;
	}
}
