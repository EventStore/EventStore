namespace EventStore.Projections.Core.Messages.Persisted.Commands {
	public sealed class SlaveProjectionsStartedResponse {
		public string CorrelationId;
		public SlaveProjectionCommunicationChannels SlaveProjections;
	}
}
