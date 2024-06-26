using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using EventStore.Projections.Core.Services;
using Microsoft.AspNetCore.Identity.Data;

namespace EventStore.Projections.Core.Metrics;

public class ProjectionStatusMetric {
	private readonly ObservableUpDownMetric<long> _statsMetric;

	public ProjectionStatusMetric(Meter meter, string name) {
		_statsMetric = new ObservableUpDownMetric<long>(meter, name);
	}

	public void Register(ProjectionStatistics statistics) {
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

		Register(_statsMetric, statistics.EffectiveName, "Faulted", () => projectionFaulted);
		Register(_statsMetric, statistics.EffectiveName, "Running", () => projectionRunning);
		Register(_statsMetric, statistics.EffectiveName, "Stopped", () => projectionStopped);
	}

	private static void Register(
		ObservableUpDownMetric<long> metric,
		string projectionName,
		string projectionStatus,
		Func<long> measurementProvider) {


		var tags = new KeyValuePair<string, object>[] {
			new("projection", projectionName),
			new("status", projectionStatus)
		};

		metric.Register(()=> new (measurementProvider(), tags));
	}
}
