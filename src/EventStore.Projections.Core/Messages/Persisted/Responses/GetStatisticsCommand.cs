using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Messages.Persisted.Responses {
	public class GetStatisticsCommand {
		public string Name;
		public ProjectionMode? Mode;
		public bool IncludeDeleted;
	}
}
