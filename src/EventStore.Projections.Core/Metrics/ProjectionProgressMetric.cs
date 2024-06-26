using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Metrics;

public class ProjectionProgressMetric {
	private readonly ObservableUpDownMetric<long> _statsMetric;

	public ProjectionProgressMetric(Meter meter, string name) {
		_statsMetric = new ObservableUpDownMetric<long>(meter, name);
	}

	public void Register(ProjectionStatistics statistics) {
		//_statsMetric.Register(()=> new (statistics.EventsProcessedAfterRestart, tags));
		Register(_statsMetric, statistics.EffectiveName, () => (long)(statistics.Progress / 100.0));
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
