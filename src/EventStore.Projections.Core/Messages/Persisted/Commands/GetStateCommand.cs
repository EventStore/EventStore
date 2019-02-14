namespace EventStore.Projections.Core.Messages.Persisted.Commands {
	public sealed class GetStateCommand {
		public string Id { get; set; }
		public string CorrelationId { get; set; }
		public string Partition { get; set; }
	}
}
