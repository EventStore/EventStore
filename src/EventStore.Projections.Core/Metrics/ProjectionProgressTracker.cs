using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Metrics;

public interface IProjectionProgressTracker {
	public void Register(ProjectionStatistics statistics);
}

public class ProjectionProgressTracker : IProjectionProgressTracker {
	private readonly ProjectionProgressMetric _metric;

	public ProjectionProgressTracker(ProjectionProgressMetric metric) {
		_metric = metric;
	}

	public void Register(ProjectionStatistics statistics) {
		_metric.Register(statistics);
	}

	public class NoOp : IProjectionProgressTracker {
		public void Register(ProjectionStatistics statistics) {}
	}
}
