// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace EventStore.Core.Metrics;

public class ObservableUpDownMetric<T> where T : struct {
	private readonly List<Func<Measurement<T>>> _measurementProviders = new();
	private readonly object _lock = new();

	public ObservableUpDownMetric(Meter meter, string name, string unit = null) {
		meter.CreateObservableUpDownCounter(name, Observe, unit);
	}

	public void Register(Func<Measurement<T>> measurementProvider) {
		if (measurementProvider is null) {
			throw new ArgumentException("Measurement provider couldn't be null");
		}

		lock (_lock) {
			_measurementProviders.Add(measurementProvider);
		}
	}

	private IEnumerable<Measurement<T>> Observe() {
		lock (_lock) {
			foreach (var measurementProvider in _measurementProviders) {
				yield return measurementProvider();
			}
		}
	}
}
