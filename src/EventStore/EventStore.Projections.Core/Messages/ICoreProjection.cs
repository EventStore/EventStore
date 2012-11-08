using EventStore.Core.Bus;

namespace EventStore.Projections.Core.Messages
{
    public interface ICoreProjection : IHandle<ProjectionSubscriptionMessage.CommittedEventReceived>,
                                       IHandle<CoreProjectionProcessingMessage.CheckpointLoaded>,
                                       IHandle<ProjectionSubscriptionMessage.CheckpointSuggested>,
                                       IHandle<ProjectionSubscriptionMessage.ProgressChanged>,
                                       IHandle<CoreProjectionProcessingMessage.CheckpointCompleted>,
                                       IHandle<CoreProjectionProcessingMessage.PauseRequested>,
                                       IHandle<CoreProjectionProcessingMessage.RestartRequested>
    {
    }
}