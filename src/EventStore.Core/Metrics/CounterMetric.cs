// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace EventStore.Core.Metrics;

public class CounterMetric {
	private readonly List<CounterSubMetric> _subMetrics = new();
	private readonly object _lock = new();

	public CounterMetric(Meter meter, string name, string unit) {
		meter.CreateObservableCounter(name, Observe, unit);
	}

	public void Add(CounterSubMetric subMetric) {
		lock (_lock) {
			_subMetrics.Add(subMetric);
		}
	}

	private IEnumerable<Measurement<long>> Observe() {
		lock (_lock) {
			foreach (CounterSubMetric subMetric in _subMetrics) {
				yield return subMetric.Observe();
			}
		}
	}
}
