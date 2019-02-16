using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages.Persisted.Responses {
	public class Prepared {
		public string Id { get; set; }
		public ProjectionSourceDefinition SourceDefinition { get; set; }
	}
}
