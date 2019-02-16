namespace EventStore.Projections.Core.Messages.Persisted.Responses.Slave {
	public class PartitionMeasuredResponse {
		public string SubscriptionId;
		public string Partition;
		public long Size;
	}
}
