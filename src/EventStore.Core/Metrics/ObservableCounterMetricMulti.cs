using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace EventStore.Core.Metrics;

// Registered providers can return multiple measurements
public class ObservableCounterMetricMulti<T> where T : struct {
	private readonly List<Func<IEnumerable<Measurement<T>>>> _measurementProviders = [];
	private readonly object _lock = new();

	public ObservableCounterMetricMulti(Meter meter, bool upDown, string name, string unit = null,
		string description = null) {

		if (upDown)
			meter.CreateObservableUpDownCounter(name, Observe, unit, description);
		else
			meter.CreateObservableCounter(name, Observe, unit, description);
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
