using EventStore.Projections.Core.Services.Processing;
using Newtonsoft.Json;

namespace EventStore.Projections.Core.Messages.Persisted.Responses.Slave {
	public class PartitionProcessingResultResponse {
		public string SubscriptionId;
		public string Partition;
		public string CausedBy;

		[JsonConverter(typeof(CheckpointTagJsonConverter))]
		public CheckpointTag Position;

		public string Result;
	}
}
