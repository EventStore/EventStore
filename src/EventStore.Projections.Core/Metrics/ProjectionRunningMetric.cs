using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Metrics;

public class ProjectionRunningMetric {
	private readonly ObservableUpDownMetric<long> _statsMetric;

	public ProjectionRunningMetric(Meter meter, string name) {
		_statsMetric = new ObservableUpDownMetric<long>(meter, name);
	}

	public void Register(ProjectionStatistics statistics) {
		//_statsMetric.Register(()=> new (statistics.EventsProcessedAfterRestart, tags));
		var projectionRunning = 0;

		if (statistics.Status.ToLower() == "running") projectionRunning = 1;
		Register(_statsMetric, statistics.EffectiveName, () => projectionRunning);
	}


	private static void Register(
		ObservableUpDownMetric<long> metric,
		string projectionName,
		Func<long> measurementProvider) {

		var tags = new KeyValuePair<string, object>[] {
			new("projection", projectionName),
		};
		metric.Register(()=> new (measurementProvider(), tags));
	}
}
