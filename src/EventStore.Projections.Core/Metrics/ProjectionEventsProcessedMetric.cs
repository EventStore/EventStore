using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using EventStore.Core.Metrics;
using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Metrics;

public class ProjectionEventsProcessedMetric {
	private readonly ObservableUpDownMetricMulti<long> _statsMetric;

	public ProjectionEventsProcessedMetric(Meter meter, string name) {
		_statsMetric = new ObservableUpDownMetricMulti<long>(meter, name);
	}

	public void Register(Func<ProjectionStatistics[]> getCurrentStatsList) {
		_statsMetric.Register(GetMeasurements);

		IEnumerable<Measurement<long>> GetMeasurements() {
			var currentStatsList = getCurrentStatsList();
			foreach (var statistics in currentStatsList) {
				yield return new(statistics.EventsProcessedAfterRestart, [
					new("projection", statistics.Name)
				]);
			}
		}
	}
}
