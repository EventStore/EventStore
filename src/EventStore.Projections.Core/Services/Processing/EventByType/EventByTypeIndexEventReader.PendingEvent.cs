using EventStore.Core.Data;

namespace EventStore.Projections.Core.Services.Processing.EventByType;

public partial class EventByTypeIndexEventReader
{
	private class PendingEvent {
		public readonly EventStore.Core.Data.ResolvedEvent ResolvedEvent;
		public readonly float Progress;
		public readonly TFPos TfPosition;

		public PendingEvent(EventStore.Core.Data.ResolvedEvent resolvedEvent, TFPos tfPosition, float progress) {
			ResolvedEvent = resolvedEvent;
			Progress = progress;
			TfPosition = tfPosition;
		}
	}
}
