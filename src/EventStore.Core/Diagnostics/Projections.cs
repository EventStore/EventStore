using System.Diagnostics.Tracing;

namespace EventStore.Core.Diagnostics {
	public enum ProjectionsMessage : int {
		None = 0,
		Other = int.MaxValue,
	}

	[EventSource(Name = "eventstore-experiments-projections")]
	class ProjectionsMessageSource : MyEventSource<ProjectionsMessage> {
		//qq we probably dont need this do we, we could make it virtual if we really needed to
		// or new might even be appropriate
		public static ProjectionsMessageSource LogJunk { get; } = new ProjectionsMessageSource();

		[Event(MessageProcessedEventId, Version = 0, Level = EventLevel.Informational, Message = "Projection " + TheLogString)]
		public void MessageProcessed(QueueId queueId, ProjectionsMessage messageTypeId, float queueMicroseconds, float processingMicroseconds) {
			MessageProcessed(queueId, (int)messageTypeId, queueMicroseconds, processingMicroseconds);
		}
	}
}
