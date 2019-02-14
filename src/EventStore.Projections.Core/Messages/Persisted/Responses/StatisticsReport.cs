using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Messages.Persisted.Responses {
	public class StatisticsReport {
		public string Id { get; set; }
		public ProjectionStatistics Statistics { get; set; }
	}
}
