using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Metrics;

public interface IProjectionTracker {
	void OnNewStats(ProjectionStatistics[] newStats);

	public static IProjectionTracker NoOp => NoOpTracker.Instance;
}

file sealed class NoOpTracker : IProjectionTracker {
	public static NoOpTracker Instance { get; } = new();

	public void OnNewStats(ProjectionStatistics[] newStats) { }
}
