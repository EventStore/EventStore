using System.Collections.Generic;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Tests.Services.core_projection.core_projection_checkpoint_manager
{
    public class FakeCoreProjection : ICoreProjection
    {
        public readonly List<ProjectionMessage.Projections.CommittedEventReceived> _committedEventReceivedMessages =
            new List<ProjectionMessage.Projections.CommittedEventReceived>();

        public readonly List<ProjectionMessage.Projections.CheckpointSuggested> _checkpointSuggestedMessages =
            new List<ProjectionMessage.Projections.CheckpointSuggested>();

        public readonly List<ProjectionMessage.Projections.CheckpointCompleted> _checkpointCompletedMessages =
            new List<ProjectionMessage.Projections.CheckpointCompleted>();

        public readonly List<ProjectionMessage.Projections.PauseRequested> _pauseRequestedMessages =
            new List<ProjectionMessage.Projections.PauseRequested>();

        public void Handle(ProjectionMessage.Projections.CommittedEventReceived message)
        {
            _committedEventReceivedMessages.Add(message);
        }

        public void Handle(ProjectionMessage.Projections.CheckpointSuggested message)
        {
            _checkpointSuggestedMessages.Add(message);
        }

        public void Handle(ProjectionMessage.Projections.CheckpointCompleted message)
        {
            _checkpointCompletedMessages.Add(message);
        }

        public void Handle(ProjectionMessage.Projections.PauseRequested message)
        {
            _pauseRequestedMessages.Add(message);
        }
    }
}