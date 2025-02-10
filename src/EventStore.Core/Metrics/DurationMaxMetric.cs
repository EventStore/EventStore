// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace EventStore.Core.Metrics;

public class DurationMaxMetric {
	private readonly List<DurationMaxTracker> _trackers = new();

	public DurationMaxMetric(Meter meter, string name) {
		// gauge rather than updowncounter because the dimensions wont make sense to sum,
		// because they are maxes and not necessarily from the same moment
		meter.CreateObservableGauge(name, Observe, "seconds");
	}

	public void Add(DurationMaxTracker tracker) {
		lock (_trackers) {
			_trackers.Add(tracker);
		}
	}

	private IEnumerable<Measurement<double>> Observe() {
		lock (_trackers) {
			foreach (var tracker in _trackers) {
				yield return tracker.Observe();
			}
		}
	}
}
