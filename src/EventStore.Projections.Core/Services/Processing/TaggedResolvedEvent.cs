using EventStore.Projections.Core.Services.Processing.Checkpointing;

namespace EventStore.Projections.Core.Services.Processing {
	public sealed class TaggedResolvedEvent {
		public readonly ResolvedEvent ResolvedEvent;
		public readonly CheckpointTag ReaderPosition;

		public TaggedResolvedEvent(ResolvedEvent resolvedEvent, CheckpointTag readerPosition) {
			ResolvedEvent = resolvedEvent;
			ReaderPosition = readerPosition;
		}
	}
}
