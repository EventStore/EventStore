using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Metrics;

public interface IProjectionStatusTracker {
	public void Register(ProjectionStatistics statistics);
}

public class ProjectionStatusTracker : IProjectionStatusTracker {
	private readonly ProjectionStatusMetric _metric;

	public ProjectionStatusTracker(ProjectionStatusMetric metric) {
		_metric = metric;
	}

	public void Register(ProjectionStatistics statistics) {
		_metric.Register(statistics);
	}

	public class NoOp : IProjectionStatusTracker {
		public void Register(ProjectionStatistics statistics) { }
	}
}
