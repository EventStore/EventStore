using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace EventStore.Core.Telemetry {
	// A metric that tracks the statuses of multiple components.
	// We are only expecting to have a handful of components.
	public class StatusMetric {
		private readonly List<StatusSubMetric> _subMetrics = new();
		private readonly IClock _clock;

		public StatusMetric(Meter meter, string name, IClock clock = null) {
			_clock = clock ?? Clock.Instance;

			// The submetrics only go up, so we use a counter
			// Observable because the value is the current time in seconds
			meter.CreateObservableCounter(name, Observe);
		}

		public void Add(StatusSubMetric subMetric) {
			lock (_subMetrics) {
				_subMetrics.Add(subMetric);
			}
		}

		public IEnumerable<Measurement<long>> Observe() {
			var secondsSinceEpoch = _clock.SecondsSinceEpoch;
			lock (_subMetrics) {
				foreach (var instance in _subMetrics) {
					yield return instance.Observe(secondsSinceEpoch);
				}
			}
		}
	}
}
