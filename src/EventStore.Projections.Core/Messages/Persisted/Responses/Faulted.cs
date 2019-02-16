namespace EventStore.Projections.Core.Messages.Persisted.Responses {
	public class Faulted {
		public string Id { get; set; }
		public string FaultedReason { get; set; }
	}
}
