using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using EventStore.Core.Metrics;
using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Metrics;

public class ProjectionProgressMetric {
	private readonly ObservableUpDownMetricMulti<float> _statsMetric;

	public ProjectionProgressMetric(Meter meter, string name) {
		_statsMetric = new ObservableUpDownMetricMulti<float>(meter, name);
	}

	public void Register(Func<ProjectionStatistics[]> getCurrentStatsList) {
		_statsMetric.Register(GetMeasurements);

		IEnumerable<Measurement<float>> GetMeasurements() {
			var currentStatsList = getCurrentStatsList();
			foreach (var statistics in currentStatsList) {
				yield return new(statistics.Progress / 100.0f, [
					new("projection", statistics.Name)
				]);
			}
		}
	}
}
