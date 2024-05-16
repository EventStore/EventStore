using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Metrics;

public class ProjectionMetrics {

	private readonly ObservableUpDownMetric<long> _statsMetric;

	public ProjectionMetrics(Meter meter, string name) {
		_statsMetric = new ObservableUpDownMetric<long>(meter, name);
	}

	public void Register(ProjectionStatistics[] statsList) {
		foreach (var projectionStat in statsList) {
			var projectionRunning = 0;

			switch (projectionStat.Status.ToLower()) {
				case "running":
					projectionRunning = 1;
					Register(_statsMetric, projectionStat.EffectiveName, "eventstore-projection-status",
						projectionStat.Status,
						() => 1);
					break;
				case "stopped":
					Register(_statsMetric, projectionStat.EffectiveName, "eventstore-projection-status",
						projectionStat.Status,
						() => 1);
					break;
				case "faulted":
					Register(_statsMetric, projectionStat.EffectiveName, "eventstore-projection-status",
						projectionStat.Status,
						() => 1);
					break;
			}

			Register(_statsMetric, projectionStat.EffectiveName, "eventstore-projection-running", null,
				() => projectionRunning);
			Register(_statsMetric, projectionStat.EffectiveName, "eventstore-projection-progress", null,
				() => (long)(projectionStat.Progress / 100.0));
			Register(_statsMetric, projectionStat.EffectiveName, "eventstore-projection-events-processed-after-restart-total",
				null, () => projectionStat.EventsProcessedAfterRestart);
		}
	}

	private void Register(ObservableUpDownMetric<long> metric, string projectionName, string metricName, string projectionStatus, Func<long> measurementProvider) {
		if (projectionStatus != null) {
			var tags = new KeyValuePair<string, object>[] {
				new("projection", projectionName),
				new("kind", metricName),
				new("status", projectionStatus)
			};
			metric.Register(()=> new (measurementProvider(), tags));
		} else {
			var tags = new KeyValuePair<string, object>[] {
				new("projection", projectionName),
				new("kind", metricName),
			};
			metric.Register(()=> new (measurementProvider(), tags));
		}
	}
}
