namespace EventStore.Projections.Core.Services.Processing {
	public sealed class TaggedResolvedEvent {
		public readonly ResolvedEvent ResolvedEvent;
		public readonly string EventCategory;
		public readonly CheckpointTag ReaderPosition;

		public TaggedResolvedEvent(ResolvedEvent resolvedEvent, string eventCategory, CheckpointTag readerPosition) {
			ResolvedEvent = resolvedEvent;
			EventCategory = eventCategory;
			ReaderPosition = readerPosition;
		}
	}
}
