using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using EventStore.Core.Metrics;
using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Metrics;

public class ProjectionRunningMetric {
	private readonly ObservableCounterMetricMulti<long> _statsMetric;

	public ProjectionRunningMetric(Meter meter, string name) {
		_statsMetric = new ObservableCounterMetricMulti<long>(meter, upDown: true, name);
	}

	public void Register(Func<ProjectionStatistics[]> getCurrentStatsList) {
		_statsMetric.Register(GetMeasurements);

		IEnumerable<Measurement<long>> GetMeasurements() {
			var currentStatsList = getCurrentStatsList();
			foreach (var statistics in currentStatsList) {
				var projectionRunning = statistics.Status.Equals("running", StringComparison.CurrentCultureIgnoreCase)
					? 1
					: 0;

				yield return new(projectionRunning, [
					new("projection", statistics.Name)
				]);
			}
		}
	}
}
