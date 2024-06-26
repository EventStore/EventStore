using EventStore.Projections.Core.Services;
namespace EventStore.Projections.Core.Metrics;

public interface IProjectionEventsProcessedAfterRestartTracker {
	public void Register(ProjectionStatistics statistics);
}

public class ProjectionEventsProcessedAfterRestartTracker : IProjectionEventsProcessedAfterRestartTracker {
	private readonly ProjectionEventsProcessedAfterRestartMetric _metric;

	public ProjectionEventsProcessedAfterRestartTracker(ProjectionEventsProcessedAfterRestartMetric metric) {
		_metric = metric;
	}

	public void Register(ProjectionStatistics statistics) {
		_metric.Register(statistics);
	}

	public class NoOp : IProjectionEventsProcessedAfterRestartTracker {
		public void Register(ProjectionStatistics statistics) { }
	}
}
