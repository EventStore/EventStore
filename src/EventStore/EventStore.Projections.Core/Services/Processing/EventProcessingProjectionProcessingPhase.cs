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
using System.Diagnostics;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class EventProcessingProjectionProcessingPhase : EventSubscriptionBasedProjectionProcessingPhase,
        IHandle<EventReaderSubscriptionMessage.CommittedEventReceived>,
        IHandle<EventReaderSubscriptionMessage.PartitionEofReached>,
        IEventProcessingProjectionPhase
    {
        private readonly IProjectionStateHandler _projectionStateHandler;
        private readonly bool _definesStateTransform;
        private readonly StatePartitionSelector _statePartitionSelector;
        private readonly bool _isBiState;

        private string _handlerPartition;
        private bool _sharedStateSet;
        private readonly Stopwatch _stopwatch;


        public EventProcessingProjectionProcessingPhase(
            CoreProjection coreProjection, Guid projectionCorrelationId, IPublisher publisher,
            ProjectionConfig projectionConfig, Action updateStatistics, IProjectionStateHandler projectionStateHandler,
            PartitionStateCache partitionStateCache, bool definesStateTransform, string projectionName, ILogger logger,
            CheckpointTag zeroCheckpointTag, ICoreProjectionCheckpointManager coreProjectionCheckpointManager,
            StatePartitionSelector statePartitionSelector, ReaderSubscriptionDispatcher subscriptionDispatcher,
            IReaderStrategy readerStrategy, IResultWriter resultWriter, bool useCheckpoints, bool stopOnEof,
            bool isBiState, bool orderedPartitionProcessing)
            : base(
                publisher, coreProjection, projectionCorrelationId, coreProjectionCheckpointManager, projectionConfig,
                projectionName, logger, zeroCheckpointTag, partitionStateCache, resultWriter, updateStatistics,
                subscriptionDispatcher, readerStrategy, useCheckpoints, stopOnEof, orderedPartitionProcessing, isBiState)
        {

            _projectionStateHandler = projectionStateHandler;
            _definesStateTransform = definesStateTransform;
            _statePartitionSelector = statePartitionSelector;
            _isBiState = isBiState;

            _stopwatch = new Stopwatch();

        }

        public void Handle(EventReaderSubscriptionMessage.CommittedEventReceived message)
        {
            //TODO:  make sure this is no longer required : if (_state != State.StateLoaded)
            if (IsOutOfOrderSubscriptionMessage(message))
                return;
            RegisterSubscriptionMessage(message);
            try
            {
                CheckpointTag eventTag = message.CheckpointTag;
                var committedEventWorkItem = new CommittedEventWorkItem(this, message, _statePartitionSelector);
                _processingQueue.EnqueueTask(committedEventWorkItem, eventTag);
                if (_state == PhaseState.Running) // prevent processing mostly one projection
                    EnsureTickPending();
            }
            catch (Exception ex)
            {
                _coreProjection.SetFaulted(ex);
            }
        }

        public void Handle(EventReaderSubscriptionMessage.PartitionEofReached message)
        {
            if (IsOutOfOrderSubscriptionMessage(message))
                return;
            RegisterSubscriptionMessage(message);
            try
            {
                var partitionCompletedWorkItem = new PartitionCompletedWorkItem(
                    this, _checkpointManager, message.Partition, message.CheckpointTag);
                _processingQueue.EnqueueTask(
                    partitionCompletedWorkItem, message.CheckpointTag, allowCurrentPosition: true);
                ProcessEvent();
            }
            catch (Exception ex)
            {
                _coreProjection.SetFaulted(ex);
            }
        }


        public EventProcessedResult ProcessCommittedEvent(
            EventReaderSubscriptionMessage.CommittedEventReceived message, string partition)
        {
            switch (_state)
            {
                case PhaseState.Running:
                    var result = InternalProcessCommittedEvent(partition, message);
                    return result;
                case PhaseState.Stopped:
                    _logger.Error("Ignoring committed event in stopped state");
                    return null;
                default:
                    throw new NotSupportedException();
            }
        }

        private EventProcessedResult InternalProcessCommittedEvent(
            string partition, EventReaderSubscriptionMessage.CommittedEventReceived message)
        {
            string newState;
            string projectionResult;
            EmittedEventEnvelope[] emittedEvents;
            //TODO: support shared state
            string newSharedState;
            var hasBeenProcessed = SafeProcessEventByHandler(
                partition, message, out newState, out newSharedState, out projectionResult, out emittedEvents);
            if (hasBeenProcessed)
            {
                var newPartitionState = new PartitionState(newState, projectionResult, message.CheckpointTag);
                var newSharedPartitionState = newSharedState != null
                    ? new PartitionState(newSharedState, null, message.CheckpointTag)
                    : null;

                return InternalCommittedEventProcessed(
                    partition, message, emittedEvents, newPartitionState, newSharedPartitionState);
            }
            return null;
        }

        private bool SafeProcessEventByHandler(
            string partition, EventReaderSubscriptionMessage.CommittedEventReceived message, out string newState,
            out string newSharedState, out string projectionResult, out EmittedEventEnvelope[] emittedEvents)
        {
            projectionResult = null;
            //TODO: not emitting (optimized) projection handlers can skip serializing state on each processed event
            bool hasBeenProcessed;
            try
            {
                hasBeenProcessed = ProcessEventByHandler(
                    partition, message, out newState, out newSharedState, out projectionResult, out emittedEvents);
            }
            catch (Exception ex)
            {
                // update progress to reflect exact fault position
                _checkpointManager.Progress(message.Progress);
                SetFaulting(
                    String.Format(
                        "The {0} projection failed to process an event.\r\nHandler: {1}\r\nEvent Position: {2}\r\n\r\nMessage:\r\n\r\n{3}",
                        _projectionName, GetHandlerTypeName(), message.CheckpointTag, ex.Message), ex);
                newState = null;
                newSharedState = null;
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
            string partition, EventReaderSubscriptionMessage.CommittedEventReceived message, out string newState,
            out string newSharedState, out string projectionResult, out EmittedEventEnvelope[] emittedEvents)
        {
            projectionResult = null;
            SetHandlerState(partition);
            _stopwatch.Start();
            var result = _projectionStateHandler.ProcessEvent(
                partition, message.CheckpointTag, message.EventCategory, message.Data, out newState, out newSharedState,
                out emittedEvents);
            if (result)
            {
                var oldState = _partitionStateCache.GetLockedPartitionState(partition);
                //TODO: depending on query processing final state to result transformation should happen either here (if EOF) on while writing results
                if ( /*_producesRunningResults && */oldState.State != newState)
                {
                    if (_definesStateTransform)
                    {
                        projectionResult = _projectionStateHandler.TransformStateToResult();
                    }
                    else
                    {
                        projectionResult = newState;
                    }
                }
                else
                {
                    projectionResult = oldState.Result;
                }
            }
            _stopwatch.Stop();
            return result;
        }

        private void SetHandlerState(string partition)
        {
            if (_handlerPartition == partition)
                return;
            var newState = _partitionStateCache.GetLockedPartitionState(partition);
            _handlerPartition = partition;
            if (newState != null && !String.IsNullOrEmpty(newState.State))
                _projectionStateHandler.Load(newState.State);
            else
                _projectionStateHandler.Initialize();
            if (!_sharedStateSet && _isBiState)
            {
                var newSharedState = _partitionStateCache.GetLockedPartitionState("");
                _projectionStateHandler.LoadShared(newSharedState.State);
                _sharedStateSet = true;
            }
        }


        public override void NewCheckpointStarted(CheckpointTag at)
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
                    var faultedReason =
                        String.Format(
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
                        _resultWriter.EventsEmitted(emittedEvents, Guid.Empty, correlationId: null);
                }
            }
        }

        public override void GetStatistics(ProjectionStatistics info)
        {
            base.GetStatistics(info);
            info.CoreProcessingTime = _stopwatch.ElapsedMilliseconds;
        }

        public override void Dispose()
        {
            if (_projectionStateHandler != null)
                _projectionStateHandler.Dispose();
        }
    }
}