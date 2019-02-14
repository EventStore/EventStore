using EventStore.Core.Bus;

namespace EventStore.Projections.Core.Messages {
	public interface ICoreProjection :
		IHandle<CoreProjectionProcessingMessage.CheckpointLoaded>,
		IHandle<CoreProjectionProcessingMessage.PrerecordedEventsLoaded>,
		IHandle<CoreProjectionProcessingMessage.CheckpointCompleted>,
		IHandle<CoreProjectionProcessingMessage.RestartRequested>,
		IHandle<CoreProjectionProcessingMessage.Failed> {
	}
}
