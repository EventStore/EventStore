using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace EventStore.Core.Metrics;

public class DurationMaxMetric {
	private readonly List<DurationMaxTracker> _trackers = new();

	public DurationMaxMetric(Meter meter, string name) {
		// gauge rather than updowncounter because the dimensions wont make sense to sum,
		// because they are maxes and not necessarily from the same moment
		meter.CreateObservableGauge(name + "-seconds", Observe);
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
