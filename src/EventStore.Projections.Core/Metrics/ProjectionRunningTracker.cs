using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Metrics;

public interface IProjectionRunningTracker {
	public void Register(ProjectionStatistics statistics);
}

public class ProjectionRunningTracker : IProjectionRunningTracker {
	private readonly ProjectionRunningMetric _metric;

	public ProjectionRunningTracker(ProjectionRunningMetric metric) {
		_metric = metric;
	}

	public void Register(ProjectionStatistics statistics) {
		_metric.Register(statistics);
	}

	public class NoOp : IProjectionRunningTracker {
		public void Register(ProjectionStatistics statistics) {}
	}
}
