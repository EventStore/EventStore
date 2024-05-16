using System.Collections.Generic;
using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Metrics;

public interface IProjectionTracker {
	public void Register(ProjectionStatistics[] statsList);
}
public class ProjectionTracker : IProjectionTracker {
	private readonly ProjectionMetrics _metrics;

	public ProjectionTracker(ProjectionMetrics metrics) {
		_metrics = metrics;
	}

	public void Register(ProjectionStatistics[] statsList) {
		_metrics.Register(statsList);
	}

	public class NoOp : IProjectionTracker {
		public void Register(ProjectionStatistics[] statsList) { }
	}
}
