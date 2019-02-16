using EventStore.Projections.Core.Services.Processing;
using Newtonsoft.Json;

namespace EventStore.Projections.Core.Messages.Persisted.Responses {
	public class ResultReport {
		public string Id { get; set; }
		public string CorrelationId { get; set; }
		public string Result { get; set; }
		public string Partition { get; set; }

		[JsonConverter(typeof(CheckpointTagJsonConverter))]
		public CheckpointTag Position { get; set; }
	}
}
