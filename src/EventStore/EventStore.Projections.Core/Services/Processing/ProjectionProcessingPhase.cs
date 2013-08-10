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
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class EventProcessingProjectionProcessingPhase : IEventProcessingProjectionPhase, IProjectionProcessingPhase
    {
        private readonly ICoreProjectionForProcessingPhase _coreProjection;
        private readonly Guid _projectionCorrelationId;
        private readonly ProjectionProcessingStrategy _projectionProcessingStrategy;
        private readonly IProjectionStateHandler _projectionStateHandler;
        private readonly CoreProjectionQueue _processingQueue;
        private PhaseState _state;
        private bool _faulted;
        private readonly ICoreProjectionCheckpointManager _checkpointManager;
        private readonly PartitionStateCache _partitionStateCache;
        private readonly bool _definesStateTransform;
        private string _handlerPartition;
        private readonly ProjectionConfig _projectionConfig;
        private readonly string _projectionName;
        private readonly ILogger _logger;
        private readonly CheckpointTag _zeroCheckpointTag;
        private readonly IResultEmitter _resultEmitter;
        private readonly StatePartitionSelector _statePartitionSelector;
        private readonly CheckpointStrategy _checkpointStrategy;
        private readonly ITimeProvider _timeProvider;

        public EventProcessingProjectionProcessingPhase(
            CoreProjection coreProjection, Guid projectionCorrelationId, IPublisher publisher, ProjectionProcessingStrategy projectionProcessingStrategy,
            ProjectionConfig projectionConfig, Action updateStatistics, IProjectionStateHandler projectionStateHandler,
            PartitionStateCache partitionStateCache, bool definesStateTransform, string projectionName, ILogger logger,
            CheckpointTag zeroCheckpointTag, IResultEmitter resultEmitter,
            ICoreProjectionCheckpointManager coreProjectionCheckpointManager,
            StatePartitionSelector statePartitionSelector, CheckpointStrategy checkpointStrategy, ITimeProvider timeProvider)
        {
            _coreProjection = coreProjection;
            _projectionCorrelationId = projectionCorrelationId;
            _projectionProcessingStrategy = projectionProcessingStrategy;
            _projectionStateHandler = projectionStateHandler;
            _partitionStateCache = partitionStateCache;
            _definesStateTransform = definesStateTransform;
            _projectionName = projectionName;
            _logger = logger;
            _zeroCheckpointTag = zeroCheckpointTag;
            _resultEmitter = resultEmitter;
            _projectionConfig = projectionConfig;
            var projectionQueue = new CoreProjectionQueue(
                projectionCorrelationId, publisher, projectionConfig.PendingEventsThreshold, updateStatistics);
            _processingQueue = projectionQueue;
            _checkpointManager = coreProjectionCheckpointManager;
            _statePartitionSelector = statePartitionSelector;
            _checkpointStrategy = checkpointStrategy;
            _timeProvider = timeProvider;
        }

        public void Handle(EventReaderSubscriptionMessage.CommittedEventReceived message)
        {
            try
            {
                CheckpointTag eventTag = message.CheckpointTag;
                var committedEventWorkItem = new CommittedEventWorkItem(this, message, _statePartitionSelector);
                _processingQueue.EnqueueTask(committedEventWorkItem, eventTag);
            }
            catch (Exception ex)
            {
                _coreProjection.SetFaulted(ex);
            }
        }

        public void Handle(EventReaderSubscriptionMessage.ProgressChanged message)
        {
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

        public void Handle(EventReaderSubscriptionMessage.NotAuthorized message)
        {
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

        public void Handle(EventReaderSubscriptionMessage.EofReached message)
        {
            try
            {
                var progressWorkItem = new CompletedWorkItem(this);
                _processingQueue.EnqueueTask(progressWorkItem, message.CheckpointTag, allowCurrentPosition: true);
                ProcessEvent();
            }
            catch (Exception ex)
            {
                _coreProjection.SetFaulted(ex);
            }
        }

        public void Handle(EventReaderSubscriptionMessage.CheckpointSuggested message)
        {
            try
            {
                if (_checkpointStrategy.UseCheckpoints)
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
                    message.Envelope, message.CorrelationId, message.ProjectionId, this, _partitionStateCache, message.Partition);
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
                    message.Envelope, message.CorrelationId, message.ProjectionId, this, message.Partition, _partitionStateCache);
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

        public void ProcessEvent()
        {
            if (_processingQueue.ProcessEvent())
                EnsureTickPending();
        }

        public void InitializeFromCheckpoint(CheckpointTag checkpointTag)
        {
            _processingQueue.InitializeQueue(checkpointTag);
            NewCheckpointStarted(checkpointTag);
        }

        public void Subscribed(Guid subscriptionId)
        {
            _processingQueue.Subscribed(subscriptionId);
        }

        public void Unsubscribed()
        {
            _processingQueue.Unsubscribed();
        }

        public void SetState(PhaseState state)
        {
            _state = state;
        }

        public int GetBufferedEventCount()
        {
            return _processingQueue.GetBufferedEventCount();
        }

        public string GetStatus()
        {
            return _processingQueue.GetStatus();
        }


        public EventProcessedResult ProcessCommittedEvent(EventReaderSubscriptionMessage.CommittedEventReceived message, string partition)
        {
            switch (_state)
            {
                case PhaseState.Running:
                    var result = InternalProcessCommittedEvent(partition, message);
                    if (_faulted)
                        _coreProjection.EnsureUnsubscribed();
                    return result;
                case PhaseState.Stopped:
                    _coreProjection.EnsureUnsubscribed();
                    return null;
                default:
                    throw new NotSupportedException();
            }
        }

        private EventProcessedResult InternalProcessCommittedEvent(string partition, EventReaderSubscriptionMessage.CommittedEventReceived message)
        {
            string newState;
            string projectionResult;
            EmittedEventEnvelope[] emittedEvents;
            var hasBeenProcessed = SafeProcessEventByHandler(
                partition, message, out newState, out projectionResult, out emittedEvents);
            if (hasBeenProcessed)
            {
                var newPartitionState = new PartitionState(newState, projectionResult, message.CheckpointTag);
                return InternalCommittedEventProcessed(partition, message, emittedEvents, newPartitionState);
            }
            return null;
        }

        private bool SafeProcessEventByHandler(
            string partition, EventReaderSubscriptionMessage.CommittedEventReceived message, out string newState,
            out string projectionResult, out EmittedEventEnvelope[] emittedEvents)
        {
            projectionResult = null;
            //TODO: not emitting (optimized) projection handlers can skip serializing state on each processed event
            bool hasBeenProcessed;
            try
            {
                hasBeenProcessed = ProcessEventByHandler(partition, message, out newState, out projectionResult, out emittedEvents);
            }
            catch (Exception ex)
            {
                // update progress to reflect exact fault position
                _checkpointManager.Progress(message.Progress);
                SetFaulting(
                    string.Format(
                        "The {0} projection failed to process an event.\r\nHandler: {1}\r\nEvent Position: {2}\r\n\r\nMessage:\r\n\r\n{3}",
                        _projectionName, GetHandlerTypeName(), message.CheckpointTag, ex.Message), ex);
                newState = null;
                emittedEvents = null;
                hasBeenProcessed = false;
            }
            newState = newState ?? "";
            return hasBeenProcessed;
        }

        private string GetHandlerTypeName()
        {
            return _projectionStateHandler.GetType().Namespace + "." + _projectionStateHandler.GetType().Name;
        }

        private bool ProcessEventByHandler(
            string partition, EventReaderSubscriptionMessage.CommittedEventReceived message, out string newState, out string projectionResult,
            out EmittedEventEnvelope[] emittedEvents)
        {
            projectionResult = null;
            SetHandlerState(partition);
            var result = _projectionStateHandler.ProcessEvent(
                partition, message.CheckpointTag, message.EventCategory, message.Data,
                out newState, out emittedEvents);
            if (result)
            {
                var oldState = _partitionStateCache.GetLockedPartitionState(partition);
                if (oldState.State != newState)
                {
                    if (_definesStateTransform)
                    {
                        projectionResult = _projectionStateHandler.TransformStateToResult();
                    }
                }
            }
            return result;
        }

        private void SetHandlerState(string partition)
        {
            if (_handlerPartition == partition)
                return;
            var newState = _partitionStateCache.GetLockedPartitionState(partition);
            _handlerPartition = partition;
            if (newState != null && !string.IsNullOrEmpty(newState.State))
                _projectionStateHandler.Load(newState.State);
            else
                _projectionStateHandler.Initialize();
        }

        private bool ValidateEmittedEvents(EmittedEventEnvelope[] emittedEvents)
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

        private void SetFaulting(string faultedReason, Exception ex = null)
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



        private EventProcessedResult InternalCommittedEventProcessed(
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
            string statePartition, CheckpointTag at, Action<PartitionState> loadCompleted,
            bool lockLoaded)
        {
            if (statePartition == "") // root is always cached
            {
                // root partition is always locked
                var state = _partitionStateCache.TryGetAndLockPartitionState(statePartition, null);
                loadCompleted(state);
            }
            else
            {
                var s = lockLoaded ? _partitionStateCache.TryGetAndLockPartitionState(
                    statePartition, at) : _partitionStateCache.TryGetPartitionState(statePartition);
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
                        EnsureTickPending();
                    };
                    _checkpointManager.BeginLoadPartitionStateAt(statePartition, at, completed);
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
                    if (result.Partition != "" && result.OldState.CausedBy == _zeroCheckpointTag)
                        _checkpointManager.NewPartition(result.Partition, eventCheckpointTag);
                    if (result.EmittedEvents != null)
                        _checkpointManager.EventsEmitted(result.EmittedEvents, result.CausedBy, result.CorrelationId);
                    if (result.NewState != null)
                    {
                        EmitRunningResults(result);
                        _checkpointManager.StateUpdated(result.Partition, result.OldState, result.NewState);
                    }
                }
                _checkpointManager.EventProcessed(eventCheckpointTag, progress);
            }
        }

        private void EmitRunningResults(EventProcessedResult result)
        {
            var oldState = result.OldState;
            var newState = result.NewState;
            if (oldState.Result != newState.Result)
            {
                var resultEvents = ResultUpdated(result.Partition, newState);
                if (resultEvents != null)
                    _checkpointManager.EventsEmitted(resultEvents, result.CausedBy, result.CorrelationId);
            }
        }

        private EmittedEventEnvelope[] ResultUpdated(string partition, PartitionState newState)
        {
            return _resultEmitter.ResultUpdated(partition, newState.Result, newState.CausedBy);
        }

        public void RecordEventOrder(ResolvedEvent resolvedEvent, CheckpointTag orderCheckpointTag, Action completed)
        {
            switch (_state)
            {
                case PhaseState.Running:
                    _checkpointManager.RecordEventOrder(resolvedEvent, orderCheckpointTag, () =>
                    {
                        completed();
                        EnsureTickPending();
                    });
                    break;
                case PhaseState.Stopped:
                    completed(); // allow collecting events for debugging
                    break;
            }
        }

        private void EnsureTickPending()
        {
            _coreProjection.EnsureTickPending();
        }

        public CheckpointTag LastProcessedEventPosition {
            get { return _coreProjection.LastProcessedEventPosition; }
        }

        public void Complete()
        {
            _coreProjection.CompletePhase();
        }

        public void SetCurrentCheckpointSuggestedWorkItem(CheckpointSuggestedWorkItem checkpointSuggestedWorkItem)
        {
            _coreProjection.SetCurrentCheckpointSuggestedWorkItem(checkpointSuggestedWorkItem);
        }

        public void NewCheckpointStarted(CheckpointTag at)
        {
            var checkpointHandler = _projectionStateHandler as IProjectionCheckpointHandler;
            if (checkpointHandler != null)
            {
                EmittedEventEnvelope[] emittedEvents;
                try
                {
                    checkpointHandler.ProcessNewCheckpoint(at, out emittedEvents);
                }
                catch (Exception ex)
                {
                    var faultedReason = string.Format(
                        "The {0} projection failed to process a checkpoint start.\r\nHandler: {1}\r\nEvent Position: {2}\r\n\r\nMessage:\r\n\r\n{3}",
                        _projectionName, GetHandlerTypeName(), at, ex.Message);
                    SetFaulting(faultedReason, ex);
                    emittedEvents = null;
                }
                if (emittedEvents != null && emittedEvents.Length > 0)
                {
                    if (!ValidateEmittedEvents(emittedEvents))
                        return;

                    if (_state == PhaseState.Running)
                        _checkpointManager.EventsEmitted(emittedEvents, Guid.Empty, correlationId: null);
                }
            }
        }

        public void SetFaulted()
        {
            _faulted = true;
        }

        public void Dispose()
        {
            if (_projectionStateHandler != null)
                _projectionStateHandler.Dispose();
        }

        public void GetStatistics(ProjectionStatistics info)
        {
            info.Status = info.Status + GetStatus();
            info.BufferedEvents += GetBufferedEventCount();
        }

        public IReaderStrategy ReaderStrategy
        {
            get { return _checkpointStrategy.ReaderStrategy; }
        }

        public ICoreProjectionCheckpointManager CheckpointManager
        {
            get { return _checkpointManager; }
        }

        public ReaderSubscriptionOptions GetSubscriptionOptions()
        {
            return new ReaderSubscriptionOptions(
                _projectionConfig.CheckpointUnhandledBytesThreshold, _projectionConfig.CheckpointHandledThreshold,
                _projectionProcessingStrategy.GetStopOnEof(), stopAfterNEvents: null);
        }
    }
}