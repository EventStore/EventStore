using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Messages
{
    public interface ICoreProjection : IHandle<EventReaderSubscriptionMessage.CommittedEventReceived>,
                                       IHandle<EventReaderSubscriptionMessage.CheckpointSuggested>,
                                       IHandle<EventReaderSubscriptionMessage.ProgressChanged>,
                                       IHandle<EventReaderSubscriptionMessage.EofReached>,
                                       IHandle<CoreProjectionProcessingMessage.CheckpointLoaded>,
                                       IHandle<CoreProjectionProcessingMessage.PrerecordedEventsLoaded>,
                                       IHandle<CoreProjectionProcessingMessage.CheckpointCompleted>,
                                       IHandle<CoreProjectionProcessingMessage.RestartRequested>,
                                       IHandle<CoreProjectionProcessingMessage.Failed>
    {
    }
}