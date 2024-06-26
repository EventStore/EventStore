using System;
using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Metrics;

public interface IProjectionTracker {
	void OnNewStats(ProjectionStatistics[] newStats);
}

public class ProjectionTracker : IProjectionTracker {
	private readonly ProjectionEventsProcessedMetric _projectionEventsProcessedMetric;
	private readonly ProjectionProgressMetric _projectionProgressMetric;
	private readonly ProjectionRunningMetric _projectionRunningMetric;
	private readonly ProjectionStatusMetric _projectionStatusMetric;

	private ProjectionStatistics[] _currentStats = [];

	public ProjectionTracker(
		ProjectionEventsProcessedMetric projectionEventsProcessedTracker,
		ProjectionProgressMetric projectionProgressTracker,
		ProjectionRunningMetric projectionRunningTracker,
		ProjectionStatusMetric projectionStatusTracker) {

		_projectionEventsProcessedMetric = projectionEventsProcessedTracker;
		_projectionProgressMetric = projectionProgressTracker;
		_projectionRunningMetric = projectionRunningTracker;
		_projectionStatusMetric = projectionStatusTracker;

		Func<ProjectionStatistics[]> getCurrentStats = () => _currentStats;
		_projectionRunningMetric.Register(getCurrentStats);
		_projectionEventsProcessedMetric.Register(getCurrentStats);
		_projectionProgressMetric.Register(getCurrentStats);
		_projectionStatusMetric.Register(getCurrentStats);
	}

	public void OnNewStats(ProjectionStatistics[] newStats) {
		_currentStats = newStats ?? [];
	}

	public class NoOp : IProjectionTracker {
		public void OnNewStats(ProjectionStatistics[] newStats) { }
	}
}
