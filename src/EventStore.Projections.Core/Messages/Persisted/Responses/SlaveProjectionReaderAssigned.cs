namespace EventStore.Projections.Core.Messages.Persisted.Responses {
	public class SlaveProjectionReaderAssigned {
		public string Id { get; set; }
		public string SubscriptionId { get; set; }
	}
}
