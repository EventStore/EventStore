// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;
using System.Diagnostics.Contracts;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public abstract class EventSubscriptionBasedProjectionProcessingPhase : IProjectionPhaseCompleter,
        IProjectionPhaseCheckpointManager,
        IHandle<EventReaderSubscriptionMessage.ProgressChanged>,
        IHandle<EventReaderSubscriptionMessage.SubscriptionStarted>,
        IHandle<EventReaderSubscriptionMessage.NotAuthorized>,
        IHandle<EventReaderSubscriptionMessage.EofReached>,
        IHandle<EventReaderSubscriptionMessage.CheckpointSuggested>,
        IHandle<EventReaderSubscriptionMessage.ReaderAssignedReader>,
        IHandle<EventReaderSubscriptionMessage.PartitionMeasured>,
        IProjectionProcessingPhase,
        IProjectionPhaseStateManager
    {
        protected readonly IPublisher _publisher;
        protected readonly ICoreProjectionForProcessingPhase _coreProjection;
        protected readonly Guid _projectionCorrelationId;
        protected readonly ICoreProjectionCheckpointManager _checkpointManager;
        protected readonly ProjectionConfig _projectionConfig;
        protected readonly string _projectionName;
        protected readonly ILogger _logger;
        protected readonly CheckpointTag _zeroCheckpointTag;
        protected readonly CoreProjectionQueue _processingQueue;
        protected readonly PartitionStateCache _partitionStateCache;
        protected readonly ReaderSubscriptionDispatcher _subscriptionDispatcher;
        protected readonly IReaderStrategy _readerStrategy;
        protected readonly IResultWriter _resultWriter;
        protected readonly bool _useCheckpoints;
        protected long _expectedSubscriptionMessageSequenceNumber = -1;
        protected Guid _currentSubscriptionId;
        protected bool _subscribed;
        protected PhaseState _state;
        protected readonly bool _stopOnEof;

        protected EventSubscriptionBasedProjectionProcessingPhase(
            IPublisher publisher, ICoreProjectionForProcessingPhase coreProjection, Guid projectionCorrelationId,
            ICoreProjectionCheckpointManager checkpointManager, ProjectionConfig projectionConfig, string projectionName,
            ILogger logger, CheckpointTag zeroCheckpointTag, PartitionStateCache partitionStateCache,
            IResultWriter resultWriter, Action updateStatistics, ReaderSubscriptionDispatcher subscriptionDispatcher,
            IReaderStrategy readerStrategy, bool useCheckpoints, bool stopOnEof)
        {
            _publisher = publisher;
            _coreProjection = coreProjection;
            _projectionCorrelationId = projectionCorrelationId;
            _checkpointManager = checkpointManager;
            _projectionConfig = projectionConfig;
            _projectionName = projectionName;
            _logger = logger;
            _zeroCheckpointTag = zeroCheckpointTag;
            _partitionStateCache = partitionStateCache;
            _resultWriter = resultWriter;
            _processingQueue = new CoreProjectionQueue(
                projectionCorrelationId, publisher, projectionConfig.PendingEventsThreshold, updateStatistics);
            _processingQueue.EnsureTickPending += EnsureTickPending;
            _subscriptionDispatcher = subscriptionDispatcher;
            _readerStrategy = readerStrategy;
            _useCheckpoints = useCheckpoints;
            _stopOnEof = stopOnEof;
        }

        public void UnlockAndForgetBefore(CheckpointTag checkpointTag)
        {
            _partitionStateCache.Unlock(checkpointTag, forgetUnlocked: true);
        }

        public CheckpointTag LastProcessedEventPosition
        {
            get { return _coreProjection.LastProcessedEventPosition; }
        }

        public ICoreProjectionCheckpointManager CheckpointManager
        {
            get { return _checkpointManager; }
        }

        protected bool IsOutOfOrderSubscriptionMessage(EventReaderSubscriptionMessage message)
        {
            if (_currentSubscriptionId != message.SubscriptionId)
                return true;
            if (_expectedSubscriptionMessageSequenceNumber != message.SubscriptionMessageSequenceNumber)
                throw new InvalidOperationException("Out of order message detected");
            return false;
        }

        protected void RegisterSubscriptionMessage(EventReaderSubscriptionMessage message)
        {
            _expectedSubscriptionMessageSequenceNumber = message.SubscriptionMessageSequenceNumber + 1;
        }

        protected void EnsureTickPending()
        {
            _coreProjection.EnsureTickPending();
        }

        public virtual void AssignSlaves(SlaveProjectionCommunicationChannels slaveProjections)
        {
            throw new NotSupportedException();
        }

        public void ProcessEvent()
        {
            _processingQueue.ProcessEvent();
        }

        public void Handle(EventReaderSubscriptionMessage.ProgressChanged message)
        {
            if (IsOutOfOrderSubscriptionMessage(message))
                return;
            RegisterSubscriptionMessage(message);
            try
            {
                var progressWorkItem = new ProgressWorkItem(_checkpointManager, message.Progress);
                _processingQueue.EnqueueTask(progressWorkItem, message.CheckpointTag, allowCurrentPosition: true);
                ProcessEvent();
            }
            catch (Exception ex)
            {
                _coreProjection.SetFaulted(ex);
            }
        }

        public void Handle(EventReaderSubscriptionMessage.SubscriptionStarted message)
        {
            if (IsOutOfOrderSubscriptionMessage(message))
                return;
            RegisterSubscriptionMessage(message);
            try
            {
                _subscriptionStartedAtLastCommitPosition = message.StartingLastCommitPosition;
            }
            catch (Exception ex)
            {
                _coreProjection.SetFaulted(ex);
            }
        }

        public void Handle(EventReaderSubscriptionMessage.NotAuthorized message)
        {
            if (IsOutOfOrderSubscriptionMessage(message))
                return;
            RegisterSubscriptionMessage(message);
            try
            {
                var progressWorkItem = new NotAuthorizedWorkItem();
                _processingQueue.EnqueueTask(progressWorkItem, message.CheckpointTag, allowCurrentPosition: true);
                ProcessEvent();
            }
            catch (Exception ex)
            {
                _coreProjection.SetFaulted(ex);
            }
        }

        public void Unsubscribed()
        {
            _subscriptionDispatcher.Cancel(_projectionCorrelationId);
            _subscribed = false;
            _processingQueue.Unsubscribed();
        }

        public void Handle(EventReaderSubscriptionMessage.EofReached message)
        {
            if (IsOutOfOrderSubscriptionMessage(message))
                return;
            RegisterSubscriptionMessage(message);
            try
            {
                Unsubscribed();
                var completedWorkItem = new CompletedWorkItem(this);
                _processingQueue.EnqueueTask(completedWorkItem, message.CheckpointTag, allowCurrentPosition: true);
                ProcessEvent();
            }
            catch (Exception ex)
            {
                _coreProjection.SetFaulted(ex);
            }
        }

        public void Handle(EventReaderSubscriptionMessage.PartitionMeasured message)
        {
            if (IsOutOfOrderSubscriptionMessage(message))
                return;
            RegisterSubscriptionMessage(message);
            try
            {
            }
            catch (Exception ex)
            {
                _coreProjection.SetFaulted(ex);
            }
        }

        public void Handle(EventReaderSubscriptionMessage.CheckpointSuggested message)
        {
            if (IsOutOfOrderSubscriptionMessage(message))
                return;
            RegisterSubscriptionMessage(message);
            try
            {
                if (_useCheckpoints)
                {
                    CheckpointTag checkpointTag = message.CheckpointTag;
                    var checkpointSuggestedWorkItem = new CheckpointSuggestedWorkItem(this, message, _checkpointManager);
                    _processingQueue.EnqueueTask(checkpointSuggestedWorkItem, checkpointTag, allowCurrentPosition: true);
                }
                ProcessEvent();
            }
            catch (Exception ex)
            {
                _coreProjection.SetFaulted(ex);
            }
        }

        public void Handle(CoreProjectionManagementMessage.GetState message)
        {
            try
            {
                var getStateWorkItem = new GetStateWorkItem(
                    message.Envelope, message.CorrelationId, message.ProjectionId, this, message.Partition);
                _processingQueue.EnqueueOutOfOrderTask(getStateWorkItem);
                ProcessEvent();
            }
            catch (Exception ex)
            {
                message.Envelope.ReplyWith(
                    new CoreProjectionManagementMessage.StateReport(
                        message.CorrelationId, _projectionCorrelationId, message.Partition, state: null, position: null,
                        exception: ex));
                _coreProjection.SetFaulted(ex);
            }
        }

        public void Handle(CoreProjectionManagementMessage.GetResult message)
        {
            try
            {
                var getResultWorkItem = new GetResultWorkItem(
                    message.Envelope, message.CorrelationId, message.ProjectionId, this, message.Partition);
                _processingQueue.EnqueueOutOfOrderTask(getResultWorkItem);
                ProcessEvent();
            }
            catch (Exception ex)
            {
                message.Envelope.ReplyWith(
                    new CoreProjectionManagementMessage.ResultReport(
                        message.CorrelationId, _projectionCorrelationId, message.Partition, result: null, position: null,
                        exception: ex));
                _coreProjection.SetFaulted(ex);
            }
        }

        protected void UnsubscribeFromPreRecordedOrderEvents()
        {
            // projectionCorrelationId is used as a subscription identifier for delivery
            // of pre-recorded order events recovered by checkpoint manager
            _subscriptionDispatcher.Cancel(_projectionCorrelationId);
        }

        public void Subscribed(Guid subscriptionId)
        {
            _processingQueue.Subscribed(subscriptionId);
        }

        public ReaderSubscriptionOptions GetSubscriptionOptions()
        {
            return new ReaderSubscriptionOptions(
                _projectionConfig.CheckpointUnhandledBytesThreshold, _projectionConfig.CheckpointHandledThreshold,
                _stopOnEof, stopAfterNEvents: null);
        }

        protected void SubscribeReaders(CheckpointTag checkpointTag)
        {
            //TODO: should we report subscribed state even if subscribing to 
            _expectedSubscriptionMessageSequenceNumber = 0;
            _currentSubscriptionId = Guid.NewGuid();
            Subscribed(_currentSubscriptionId);
            try
            {
                var readerStrategy = _readerStrategy;
                if (readerStrategy != null)
                {
                    _subscribed = true;
                    _subscriptionDispatcher.PublishSubscribe(
                        new ReaderSubscriptionManagement.Subscribe(
                            _currentSubscriptionId, checkpointTag, readerStrategy, GetSubscriptionOptions()), this);
                }
                else
                {
                    _coreProjection.Subscribed();
                }
            }
            catch (Exception ex)
            {
                _coreProjection.SetFaulted(ex);
                return;
            }
        }

        public void SubscribeToPreRecordedOrderEvents()
        {
            var coreProjection = (CoreProjection) _coreProjection;
            // projectionCorrelationId is used as a subscription identifier for delivery
            // of pre-recorded order events recovered by checkpoint manager
            _expectedSubscriptionMessageSequenceNumber = 0;
            _currentSubscriptionId = coreProjection._projectionCorrelationId;
            _subscriptionDispatcher.Subscribed(coreProjection._projectionCorrelationId, coreProjection);
            _subscribed = true; // even if it is not a real subscription we need to unsubscribe 

        }

        public virtual void Subscribe(CheckpointTag from, bool fromCheckpoint)
        {
            Contract.Assert(_checkpointManager.LastProcessedEventPosition == @from);
            if (fromCheckpoint)
            {
                SubscribeToPreRecordedOrderEvents();
                _checkpointManager.BeginLoadPrerecordedEvents(@from);
            }
            else
                SubscribeReaders(@from);
        }

        public void Handle(CoreProjectionProcessingMessage.PrerecordedEventsLoaded message)
        {
            UnsubscribeFromPreRecordedOrderEvents();
            SubscribeReaders(message.CheckpointTag);
        }

        public CheckpointTag AdjustTag(CheckpointTag tag)
        {
            return _readerStrategy.PositionTagger.AdjustTag(tag);
        }

        protected void SetFaulting(string faultedReason, Exception ex = null)
        {
            if (_logger != null)
            {
                if (ex != null)
                    _logger.ErrorException(ex, faultedReason);
                else
                    _logger.Error(faultedReason);
            }
            _coreProjection.SetFaulting(faultedReason);
        }

        protected bool ValidateEmittedEvents(EmittedEventEnvelope[] emittedEvents)
        {
            if (!_projectionConfig.EmitEventEnabled)
            {
                if (emittedEvents != null && emittedEvents.Length > 0)
                {
                    SetFaulting("'emit' is not allowed by the projection/configuration/mode");
                    return false;
                }
            }
            return true;
        }

        public abstract void NewCheckpointStarted(CheckpointTag at);

        public void InitializeFromCheckpoint(CheckpointTag checkpointTag)
        {
            _wasReaderAssigned = false;
            // this can be old checkpoint
            var adjustedCheckpointTag = _readerStrategy.PositionTagger.AdjustTag(checkpointTag);
            _processingQueue.InitializeQueue(adjustedCheckpointTag);
            NewCheckpointStarted(adjustedCheckpointTag);
        }

        public int GetBufferedEventCount()
        {
            return _processingQueue.GetBufferedEventCount();
        }

        public string GetStatus()
        {
            return _processingQueue.GetStatus();
        }

        protected EventProcessedResult InternalCommittedEventProcessed(
            string partition, EventReaderSubscriptionMessage.CommittedEventReceived message,
            EmittedEventEnvelope[] emittedEvents, PartitionState newPartitionState)
        {
            if (!ValidateEmittedEvents(emittedEvents))
                return null;
            var oldState = _partitionStateCache.GetLockedPartitionState(partition);

            bool eventsWereEmitted = emittedEvents != null;
            bool changed = oldState.IsChanged(newPartitionState);

            PartitionState partitionState1 = null;
            // NOTE: projectionResult cannot change independently unless projection definition has changed
            if (changed)
            {
                var lockPartitionStateAt = partition != "" ? message.CheckpointTag : null;
                partitionState1 = newPartitionState;
                _partitionStateCache.CacheAndLockPartitionState(partition, partitionState1, lockPartitionStateAt);
            }
            if (changed || eventsWereEmitted)
            {
                var correlationId = message.Data.IsJson ? message.Data.Metadata.ParseCheckpointTagCorrelationId() : null;
                return new EventProcessedResult(
                    partition, message.CheckpointTag, oldState, partitionState1, emittedEvents, message.Data.EventId,
                    correlationId);
            }

            else return null;
        }

        public void BeginGetPartitionStateAt(
            string statePartition, CheckpointTag at, Action<PartitionState> loadCompleted, bool lockLoaded)
        {
            if (statePartition == "") // root is always cached
            {
                // root partition is always locked
                var state = _partitionStateCache.TryGetAndLockPartitionState(statePartition, null);
                loadCompleted(state);
            }
            else
            {
                var s = lockLoaded
                    ? _partitionStateCache.TryGetAndLockPartitionState(statePartition, at)
                    : _partitionStateCache.TryGetPartitionState(statePartition);
                if (s != null)
                    loadCompleted(s);
                else
                {
                    Action<PartitionState> completed = state =>
                    {
                        if (lockLoaded)
                            _partitionStateCache.CacheAndLockPartitionState(statePartition, state, at);
                        else
                            _partitionStateCache.CachePartitionState(statePartition, state);
                        loadCompleted(state);
                    };
                    if (_projectionConfig.CheckpointsEnabled)
                    {
                        _checkpointManager.BeginLoadPartitionStateAt(statePartition, at, completed);
                    }
                    else
                    {
                        var state = new PartitionState("", null, _zeroCheckpointTag);
                        completed(state);
                    }
                }
            }
        }

        public void FinalizeEventProcessing(
            EventProcessedResult result, CheckpointTag eventCheckpointTag, float progress)
        {
            if (_state == PhaseState.Running)
            {
                //TODO: move to separate projection method and cache result in work item
                if (result != null)
                {
                    _resultWriter.AccountPartition(result);
                    if (_projectionConfig.EmitEventEnabled && result.EmittedEvents != null)
                        _resultWriter.EventsEmitted(result.EmittedEvents, result.CausedBy, result.CorrelationId);
                    if (result.NewState != null)
                    {
                        _resultWriter.WriteRunningResult(result);
                        _checkpointManager.StateUpdated(result.Partition, result.OldState, result.NewState);
                    }
                }
                _checkpointManager.EventProcessed(eventCheckpointTag, progress);
            }
        }

        public void EmitEofResult(
            string partition, string resultBody, CheckpointTag causedBy, Guid causedByGuid, string correlationId)
        {
            _resultWriter.WriteEofResult(
                _currentSubscriptionId, partition, resultBody, causedBy, causedByGuid, correlationId);
        }

        public void RecordEventOrder(ResolvedEvent resolvedEvent, CheckpointTag orderCheckpointTag, Action completed)
        {
            switch (_state)
            {
                case PhaseState.Running:
                    _checkpointManager.RecordEventOrder(
                        resolvedEvent, orderCheckpointTag, completed);
                    break;
                case PhaseState.Stopped:
                    _logger.Error("Should not receive events in stopped state anymore");
                    completed(); // allow collecting events for debugging
                    break;
            }
        }

        public void Complete()
        {
            //NOTE: no need for EnsureUnsubscribed  as EOF
            Unsubscribed();
            _coreProjection.CompletePhase();
        }

        public void SetCurrentCheckpointSuggestedWorkItem(CheckpointSuggestedWorkItem checkpointSuggestedWorkItem)
        {
            _coreProjection.SetCurrentCheckpointSuggestedWorkItem(checkpointSuggestedWorkItem);
        }

        public virtual void GetStatistics(ProjectionStatistics info)
        {
            info.Status = info.Status + GetStatus();
            info.BufferedEvents += GetBufferedEventCount();
        }

        public CheckpointTag MakeZeroCheckpointTag()
        {
            return _zeroCheckpointTag;
        }

        public void EnsureUnsubscribed()
        {
            if (_subscribed)
            {
                Unsubscribed();
                // this was we distinguish pre-recorded events subscription
                if (_currentSubscriptionId != _projectionCorrelationId)
                    _publisher.Publish(new ReaderSubscriptionManagement.Unsubscribe(_currentSubscriptionId));
                _subscribed = false;
            }
        }

        private bool _wasReaderAssigned = false;

        protected long _subscriptionStartedAtLastCommitPosition;

        public void Handle(EventReaderSubscriptionMessage.ReaderAssignedReader message)
        {
            if (_wasReaderAssigned)
                return;
            _wasReaderAssigned = true;
            if (_projectionConfig.IsSlaveProjection)
                _publisher.Publish(
                    new CoreProjectionManagementMessage.SlaveProjectionReaderAssigned(
                        _projectionCorrelationId, message.SubscriptionId, message.ReaderId));
            _coreProjection.Subscribed();
        }


        public abstract void Dispose();

        public void SetProjectionState(PhaseState state)
        {
            _state = state;
            _processingQueue.SetIsRunning(state == PhaseState.Running);
        }
    }
}
