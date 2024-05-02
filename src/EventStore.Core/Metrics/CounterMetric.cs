﻿using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace EventStore.Core.Metrics {
	public class CounterMetric {
		private readonly List<CounterSubMetric> _subMetrics = new();
		private readonly object _lock = new();

		public CounterMetric(Meter meter, string name, string unit) {
			if (!string.IsNullOrWhiteSpace(unit)) {
				name = name + "-" + unit;
			}

			meter.CreateObservableCounter(name, Observe);
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
}
