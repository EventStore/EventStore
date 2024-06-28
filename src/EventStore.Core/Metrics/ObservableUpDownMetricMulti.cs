using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace EventStore.Core.Metrics;

// Registered providers can return multiple measurements
public class ObservableUpDownMetricMulti<T> where T : struct {
	private readonly List<Func<IEnumerable<Measurement<T>>>> _measurementProviders = [];
	private readonly object _lock = new();

	public ObservableUpDownMetricMulti(Meter meter, string name, string unit = null) {
		meter.CreateObservableUpDownCounter(name, Observe, unit);
	}

	public void Register(Func<IEnumerable<Measurement<T>>> measurementProvider) {
		if (measurementProvider is null) {
			throw new ArgumentException("Measurement provider cannot be null");
		}

		lock (_lock) {
			_measurementProviders.Add(measurementProvider);
		}
	}

	private IEnumerable<Measurement<T>> Observe() {
		lock (_lock) {
			foreach (var measurementProvider in _measurementProviders) {
				var measures = measurementProvider();
				foreach (var measure in measures) {
					yield return measure;
				}
			}
		}
	}
}
