namespace EventStore.Projections.Core.Services.Processing {
	public sealed class EmittedEventEnvelope {
		public readonly EmittedEvent Event;
		public readonly EmittedStream.WriterConfiguration.StreamMetadata StreamMetadata;

		public EmittedEventEnvelope(
			EmittedEvent @event, EmittedStream.WriterConfiguration.StreamMetadata streamMetadata = null) {
			Event = @event;
			StreamMetadata = streamMetadata;
		}
	}
}
