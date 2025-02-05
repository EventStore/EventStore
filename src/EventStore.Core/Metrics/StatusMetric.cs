// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Diagnostics.Metrics;
using EventStore.Core.Time;

namespace EventStore.Core.Metrics;

// A metric that tracks the statuses of multiple components.
// We are only expecting to have a handful of components.
public class StatusMetric {
	private readonly List<StatusSubMetric> _subMetrics = new();
	private readonly IClock _clock;

	public StatusMetric(Meter meter, string name, IClock clock = null) {
		_clock = clock ?? Clock.Instance;
		meter.CreateObservableGauge(name, Observe);
	}

	public void Add(StatusSubMetric subMetric) {
		lock (_subMetrics) {
			_subMetrics.Add(subMetric);
		}
	}

	private IEnumerable<Measurement<long>> Observe() {
		var secondsSinceEpoch = _clock.SecondsSinceEpoch;
		lock (_subMetrics) {
			foreach (var instance in _subMetrics) {
				yield return instance.Observe(secondsSinceEpoch);
			}
		}
	}
}
