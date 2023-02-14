using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace EventStore.Core.Telemetry;

public class DurationMaxMetric {
	private readonly List<DurationMaxTracker> _trackers = new();

	public DurationMaxMetric(Meter meter, string name) {
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
