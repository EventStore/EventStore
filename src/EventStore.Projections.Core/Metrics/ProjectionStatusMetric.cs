using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using EventStore.Core.Metrics;
using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Metrics;

public class ProjectionStatusMetric {
	private readonly ObservableUpDownMetricMulti<long> _statsMetric;

	public ProjectionStatusMetric(Meter meter, string name) {
		_statsMetric = new ObservableUpDownMetricMulti<long>(meter, name);
	}

	public void Register(Func<ProjectionStatistics[]> getCurrentStatsList) {
		_statsMetric.Register(GetMeasurements);

		IEnumerable<Measurement<long>> GetMeasurements() {
			var currentStatsList = getCurrentStatsList();
			foreach (var statistics in currentStatsList) {
				var projectionRunning = 0;
				var projectionFaulted = 0;
				var projectionStopped = 0;

				switch (statistics.Status.ToLower()) {
					case "running":
						projectionRunning = 1;
						break;
					case "stopped":
						projectionStopped = 1;
						break;
					case "faulted":
						projectionFaulted = 1;
						break;
				}

				yield return new(projectionRunning, [
					new("projection", statistics.Name),
					new("status", "Running"),
				]);

				yield return new(projectionFaulted, [
					new("projection", statistics.Name),
					new("status", "Faulted"),
				]);

				yield return new(projectionStopped, [
					new("projection", statistics.Name),
					new("status", "Stopped"),
				]);
			}
		}
	}
}
