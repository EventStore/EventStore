using System;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{

    public enum PhaseState
    {
        Unknown,
        Stopped,
        Running
    }


    public interface IProjectionProcessingPhase : IDisposable
    {
        void Handle(EventReaderSubscriptionMessage.CommittedEventReceived message);
        void Handle(EventReaderSubscriptionMessage.ProgressChanged message);
        void Handle(EventReaderSubscriptionMessage.NotAuthorized message);
        void Handle(EventReaderSubscriptionMessage.EofReached message);
        void Handle(EventReaderSubscriptionMessage.CheckpointSuggested message);

        void Handle(CoreProjectionManagementMessage.GetState message);
        void Handle(CoreProjectionManagementMessage.GetResult message);

        void Initialize();
        void InitializeFromCheckpoint(CheckpointTag checkpointTag);

        void ProcessEvent();

        void Subscribed(Guid subscriptionId);
        void Unsubscribed();

        void SetState(PhaseState state);
        void SetFaulted();

        void GetStatistics(ProjectionStatistics info);
        IReaderStrategy ReaderStrategy { get; }
        ICoreProjectionCheckpointManager CheckpointManager { get; }
        ReaderSubscriptionOptions GetSubscriptionOptions();
    }
}