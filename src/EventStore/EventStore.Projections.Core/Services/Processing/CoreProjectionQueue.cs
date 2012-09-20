using System;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class CoreProjectionQueue
    {
        private readonly CoreProjection _coreProjection;

        private readonly StagedProcessingQueue _queuePendingEvents =
            new StagedProcessingQueue(
                new[] {false /* load foreach state */, false /* process Js */, true /* write emits */});

        private readonly ProjectionConfig _projectionConfig;
        private QueueState _queueState;
        private readonly CheckpointStrategy _checkpointStrategy;
        private CheckpointTag _lastEnqueuedEventTag;

        public CoreProjectionQueue(CoreProjection coreProjection)
        {
            _coreProjection = coreProjection;
            _projectionConfig = coreProjection._projectionConfig;
            _checkpointStrategy = coreProjection._checkpointStrategy;
        }

        public void ProcessEvent()
        {
            if (_queuePendingEvents.Count > 0)
                ProcessOneEvent(_coreProjection);
        }

        private void ProcessOneEvent(CoreProjection coreProjection)
        {
            try
            {
                int pendingEventsCount = _queuePendingEvents.Count;
                if (pendingEventsCount > _projectionConfig.PendingEventsThreshold)
                    coreProjection.PauseSubscription();
                if (coreProjection._subscriptionPaused && pendingEventsCount < _projectionConfig.PendingEventsThreshold/2)
                    coreProjection.ResumeSubscription();
                int processed = _queuePendingEvents.Process();
                if (processed > 0)
                    coreProjection.EnsureTickPending();
            }
            catch (Exception ex)
            {
                coreProjection.SetFaulted(ex);
            }
        }

        public int GetBufferedEventCount()
        {
            return _queuePendingEvents.Count;
        }

        public void SetRunning()
        {
            _queueState = QueueState.Running;
        }

        public void SetPaused()
        {
            _queueState = QueueState.Running;
        }

        public void SetStopped()
        {
            _queueState = QueueState.Running;
        }

        private enum QueueState
        {
            Stopped,
            Paused,
            Running
        }

        public void HandleCommittedEventReceived(CoreProjection coreProjection, ProjectionMessage.Projections.CommittedEventReceived message)
        {
            if (_queueState == QueueState.Stopped)
                return;
            CheckpointTag eventTag = _checkpointStrategy.PositionTagger.MakeCheckpointTag(message);
            ValidateQueueingOrder(eventTag);
            string partition = _checkpointStrategy.StatePartitionSelector.GetStatePartition(message);
            _queuePendingEvents.Enqueue(
                new CoreProjection.CommittedEventWorkItem(coreProjection, message, partition, eventTag));
            if (_queueState == QueueState.Running)
                ProcessOneEvent(coreProjection);
        }

        public void HandleCheckpointSuggested(CoreProjection coreProjection, ProjectionMessage.Projections.CheckpointSuggested message)
        {
            if (_queueState == QueueState.Stopped)
                return;
            if (coreProjection._projectionConfig.CheckpointsEnabled)
            {
                CheckpointTag checkpointTag = message.CheckpointTag;
                ValidateQueueingOrder(checkpointTag);
                _queuePendingEvents.Enqueue(
                    new CoreProjection.CheckpointSuggestedWorkItem(coreProjection, message, checkpointTag));
            }
            if (_queueState == QueueState.Running)
                ProcessOneEvent(coreProjection);
        }

        public void InitializeQueue(CoreProjection coreProjection)
        {
            _lastEnqueuedEventTag = coreProjection._checkpointStrategy.PositionTagger.MakeZeroCheckpointTag();
        }

        private void ValidateQueueingOrder(CheckpointTag eventTag)
        {
            if (eventTag <= _lastEnqueuedEventTag)
                throw new InvalidOperationException("Invalid order.  Last known tag is: '{0}'.  Current tag is: '{1}'");
            _lastEnqueuedEventTag = eventTag;
        }
    }
}