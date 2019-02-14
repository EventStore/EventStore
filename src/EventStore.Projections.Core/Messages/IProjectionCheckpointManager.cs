using EventStore.Core.Bus;

namespace EventStore.Projections.Core.Messages {
	public interface IProjectionCheckpointManager : IHandle<CoreProjectionProcessingMessage.ReadyForCheckpoint>,
		IHandle<CoreProjectionProcessingMessage.RestartRequested>,
		IHandle<CoreProjectionProcessingMessage.Failed> {
	}

	public interface IEmittedStreamContainer : IProjectionCheckpointManager,
		IHandle<CoreProjectionProcessingMessage.EmittedStreamAwaiting>,
		IHandle<CoreProjectionProcessingMessage.EmittedStreamWriteCompleted> {
	}
}
