using EventStore.Projections.Core.Services.Processing;
using Newtonsoft.Json;

namespace EventStore.Projections.Core.Messages.Persisted.Responses {
	public class StateReport {
		public string Id { get; set; }
		public string CorrelationId { get; set; }
		public string State { get; set; }
		public string Partition { get; set; }

		[JsonConverter(typeof(CheckpointTagJsonConverter))]
		public CheckpointTag Position { get; set; }
	}
}
