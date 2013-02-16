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
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Text;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Util;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public abstract class CoreProjectionCheckpointManager : IProjectionCheckpointManager, ICoreProjectionCheckpointManager
    {
        private readonly IPublisher _publisher;

        protected readonly
            RequestResponseDispatcher<ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted> _readDispatcher;

        protected readonly RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted> _writeDispatcher;

        protected readonly string _name;
        private readonly PositionTagger _positionTagger;
        protected readonly ProjectionNamesBuilder _namingBuilder;
        private readonly bool _useCheckpoints;
        private readonly bool _emitStateUpdated;
        private readonly bool _emitPartitionCheckpoints;
        protected readonly ILogger _logger;

        private readonly ICoreProjection _coreProjection;
        private readonly Guid _projectionCorrelationId;
        private readonly ProjectionConfig _projectionConfig;


        private ProjectionCheckpoint _currentCheckpoint;
        private ProjectionCheckpoint _closingCheckpoint;
        private int _handledEventsAfterCheckpoint;
        private CheckpointTag _requestedCheckpointPosition;
        private bool _inCheckpoint;
        private string _requestedCheckpointState;
        private CheckpointTag _lastCompletedCheckpointPosition;
        private readonly PositionTracker _lastProcessedEventPosition;
        private float _lastProcessedEventProgress;

        private int _eventsProcessedAfterRestart;
        private bool _stateLoaded;
        private bool _started;
        protected bool _stopping;
        private bool _stopped;
        private bool _stateRequested;
        private string _currentProjectionState;
        private PartitionStateUpdateManager _partitionStateUpdateManager;
        private int _readRequestsInProgress;
        private readonly HashSet<Guid> _loadStateRequests = new HashSet<Guid>();

        protected CoreProjectionCheckpointManager(
            ICoreProjection coreProjection, IPublisher publisher, Guid projectionCorrelationId,
            RequestResponseDispatcher<ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted> readDispatcher,
            RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted> writeDispatcher,
            ProjectionConfig projectionConfig, string name, PositionTagger positionTagger,
            ProjectionNamesBuilder namingBuilder, bool useCheckpoints, bool emitStateUpdated,
            bool emitPartitionCheckpoints)
        {
            if (coreProjection == null) throw new ArgumentNullException("coreProjection");
            if (publisher == null) throw new ArgumentNullException("publisher");
            if (readDispatcher == null) throw new ArgumentNullException("readDispatcher");
            if (writeDispatcher == null) throw new ArgumentNullException("writeDispatcher");
            if (projectionConfig == null) throw new ArgumentNullException("projectionConfig");
            if (name == null) throw new ArgumentNullException("name");
            if (positionTagger == null) throw new ArgumentNullException("positionTagger");
            if (namingBuilder == null) throw new ArgumentNullException("namingBuilder");
            if (name == "") throw new ArgumentException("name");

            if (emitPartitionCheckpoints && emitStateUpdated)
                throw new InvalidOperationException("EmitPartitionCheckpoints && EmitStateUpdated cannot be both set");
            _lastProcessedEventPosition = new PositionTracker(positionTagger);
            _coreProjection = coreProjection;
            _publisher = publisher;
            _projectionCorrelationId = projectionCorrelationId;
            _readDispatcher = readDispatcher;
            _writeDispatcher = writeDispatcher;
            _projectionConfig = projectionConfig;
            _logger = LogManager.GetLoggerFor<CoreProjectionCheckpointManager>();
            _name = name;
            _positionTagger = positionTagger;
            _namingBuilder = namingBuilder;
            _useCheckpoints = useCheckpoints;
            _emitStateUpdated = emitStateUpdated;
            _emitPartitionCheckpoints = emitPartitionCheckpoints;
        }

        public virtual void Initialize()
        {
            if (_currentCheckpoint != null) _currentCheckpoint.Dispose();
            if (_closingCheckpoint != null) _closingCheckpoint.Dispose();
            _currentCheckpoint = null;
            _closingCheckpoint = null;
            _handledEventsAfterCheckpoint = 0;
            _requestedCheckpointPosition = null;
            _inCheckpoint = false;
            _requestedCheckpointState = null;
            _lastCompletedCheckpointPosition = null;
            _lastProcessedEventPosition.Initialize();
            _lastProcessedEventProgress = -1;

            _eventsProcessedAfterRestart = 0;
            _stateLoaded = false;
            _started = false;
            _stopping = false;
            _stopped = false;
            _stateRequested = false;
            _currentProjectionState = null;

            foreach (var requestId in _loadStateRequests)
                _readDispatcher.Cancel(requestId);
            _loadStateRequests.Clear();
            _partitionStateUpdateManager = null;
            _readRequestsInProgress = 0;
        }

        public virtual void Start(CheckpointTag checkpointTag)
        {
            Contract.Requires(_currentCheckpoint == null);
            if (!_stateLoaded)
                throw new InvalidOperationException("State is not loaded");
            if (_started)
                throw new InvalidOperationException("Already started");
            _started = true;
            _lastProcessedEventPosition.UpdateByCheckpointTagInitial(checkpointTag);
            _lastProcessedEventProgress = -1;
            _lastCompletedCheckpointPosition = checkpointTag;
            _requestedCheckpointPosition = null;
            _currentCheckpoint = new ProjectionCheckpoint(_readDispatcher, _writeDispatcher, this, _lastProcessedEventPosition.LastTag,
                _positionTagger.MakeZeroCheckpointTag(), _projectionConfig.MaxWriteBatchLength, _logger);
            _currentCheckpoint.Start();
        }

        public void Stopping()
        {
            EnsureStarted();
            if (_stopping)
                throw new InvalidOperationException("Already stopping");
            _stopping = true;
        }

        public void Stopped()
        {
            EnsureStarted();
            _started = false;
            _stopped = true;
        }

        protected void PrerecordedEventsLoaded(CheckpointTag checkpointTag)
        {
            _coreProjection.Handle(
                new CoreProjectionProcessingMessage.PrerecordedEventsLoaded(_projectionCorrelationId, checkpointTag));
        }

        public virtual void GetStatistics(ProjectionStatistics info)
        {
            info.Position = _lastProcessedEventPosition.LastTag;
            info.Progress = _lastProcessedEventProgress;
            info.LastCheckpoint = String.Format(CultureInfo.InvariantCulture, "{0}", _lastCompletedCheckpointPosition);
            info.EventsProcessedAfterRestart = _eventsProcessedAfterRestart;
            info.WritePendingEventsBeforeCheckpoint = _closingCheckpoint != null
                                                          ? _closingCheckpoint.GetWritePendingEvents()
                                                          : 0;
            info.WritePendingEventsAfterCheckpoint = (_currentCheckpoint != null
                                                          ? _currentCheckpoint.GetWritePendingEvents()
                                                          : 0);
            info.ReadsInProgress = /*_readDispatcher.ActiveRequestCount*/ + _readRequestsInProgress
                                                                          + + (_closingCheckpoint != null
                                                                                   ? _closingCheckpoint
                                                                                         .GetReadsInProgress()
                                                                                   : 0)
                                                                          + (_currentCheckpoint != null
                                                                                 ? _currentCheckpoint.GetReadsInProgress
                                                                                       ()
                                                                                 : 0);
            info.WritesInProgress = (_closingCheckpoint != null ? _closingCheckpoint.GetWritesInProgress() : 0)
                                    + (_currentCheckpoint != null ? _currentCheckpoint.GetWritesInProgress() : 0);
            info.CheckpointStatus = _inCheckpoint ? "Requested" : "";
        }

        public void RequestCheckpointToStop()
        {
            EnsureStarted();
            if (!_stopping)
                throw new InvalidOperationException("Not stopping");
            // do not request checkpoint if no events were processed since last checkpoint
            if (_useCheckpoints
                && _lastCompletedCheckpointPosition < _lastProcessedEventPosition.LastTag)
            {
                RequestCheckpoint(_lastProcessedEventPosition);
                return;
            }
            _coreProjection.Handle(
                new CoreProjectionProcessingMessage.CheckpointCompleted(_lastCompletedCheckpointPosition));
        }

        public void StateUpdated(string partition, PartitionStateCache.State oldState, PartitionStateCache.State newState)
        {
            if (_emitStateUpdated)
                EmitStateUpdatedEventsIfAny(partition, oldState, newState);
            if (_emitPartitionCheckpoints && partition != "")
                CaptureParitionStateUpdated(partition, oldState, newState);
            if (partition == "" && newState != null) // ignore non-root partitions and non-changed states
                _currentProjectionState = newState.Data;
            EnsureStarted();
            if (_stopping)
                throw new InvalidOperationException("Stopping");
        }

        private void CaptureParitionStateUpdated(string partition, PartitionStateCache.State oldState, PartitionStateCache.State newState)
        {
            if (_partitionStateUpdateManager == null)
                _partitionStateUpdateManager = new PartitionStateUpdateManager(_namingBuilder);
            _partitionStateUpdateManager.StateUpdated(partition, newState.Data, oldState.CausedBy, newState.CausedBy);
        }

        public void EventProcessed(CheckpointTag checkpointTag, float progress)
        {
            EnsureStarted();
            if (_stopping)
                throw new InvalidOperationException("Stopping");
            _eventsProcessedAfterRestart++;
            _lastProcessedEventPosition.UpdateByCheckpointTagForward(checkpointTag);
            _lastProcessedEventProgress = progress;
            // running state only
            _handledEventsAfterCheckpoint++;
            ProcessCheckpoints();
        }

        public void EventsEmitted(EmittedEvent[] scheduledWrites)
        {
            EnsureStarted();
            if (_stopping)
                throw new InvalidOperationException("Stopping");
            if (scheduledWrites != null)
                _currentCheckpoint.ValidateOrderAndEmitEvents(scheduledWrites);
        }

        public void CheckpointSuggested(CheckpointTag checkpointTag, float progress)
        {
            if (!_useCheckpoints)
                throw new InvalidOperationException("Checkpoints are not used");
            if (_stopped || _stopping)
                return;
            EnsureStarted();
            _lastProcessedEventPosition.UpdateByCheckpointTagForward(checkpointTag);
            _lastProcessedEventProgress = progress;
            RequestCheckpoint(_lastProcessedEventPosition);
        }

        public void Progress(float progress)
        {
            if (_stopping || _stopped)
                return;
            EnsureStarted();
            _lastProcessedEventProgress = progress;
        }

        public void BeginLoadState()
        {
            if (_stateRequested)
                throw new InvalidOperationException("State has been already requested");
            BeforeBeginLoadState();
            _stateRequested = true;
            if (_useCheckpoints)
            {
                RequestLoadState();
            }
            else
            {
                CheckpointLoaded(null, null);
            }
        }

        protected void EnsureStarted()
        {
            if (!_started)
                throw new InvalidOperationException("Not started");
        }

        private void RequestCheckpoint(PositionTracker lastProcessedEventPosition)
        {
            if (!_useCheckpoints)
                throw new InvalidOperationException("Checkpoints are not allowed");
            if (!_inCheckpoint)
                StartCheckpoint(lastProcessedEventPosition, _currentProjectionState);
        }

        private void StartCheckpoint(PositionTracker lastProcessedEventPosition, string projectionState)
        {
            Contract.Requires(_closingCheckpoint == null);
            CheckpointTag requestedCheckpointPosition = lastProcessedEventPosition.LastTag;
            if (requestedCheckpointPosition == _lastCompletedCheckpointPosition)
                return; // either suggested or requested to stop

            if (_emitPartitionCheckpoints && _partitionStateUpdateManager != null)
            {
                _partitionStateUpdateManager.EmitEvents(_currentCheckpoint);
                _partitionStateUpdateManager = null;
            }

            _inCheckpoint = true;
            _requestedCheckpointPosition = requestedCheckpointPosition;
            _requestedCheckpointState = projectionState;
            _handledEventsAfterCheckpoint = 0;
            _closingCheckpoint = _currentCheckpoint;
            _currentCheckpoint = new ProjectionCheckpoint(_readDispatcher, _writeDispatcher, this, requestedCheckpointPosition,
                _positionTagger.MakeZeroCheckpointTag(), _projectionConfig.MaxWriteBatchLength, _logger);
            // checkpoint only after assigning new current checkpoint, as it may call back immediately
            _closingCheckpoint.Prepare(requestedCheckpointPosition);
        }

        private void ProcessCheckpoints()
        {
            if (_useCheckpoints)
                if (_handledEventsAfterCheckpoint >= _projectionConfig.CheckpointHandledThreshold)
                    RequestCheckpoint(_lastProcessedEventPosition);
                else
                {
                    // TODO: projections emitting events without checkpoints will eat memory by creating new emitted streams  
                }
        }

        protected void CheckpointLoaded(CheckpointTag checkpointTag, string checkpointData)
        {
            if (checkpointTag == null) // no checkpoint data found
            {
                checkpointTag = _positionTagger.MakeZeroCheckpointTag();
                checkpointData = "";
            }
            _stateLoaded = true;
            _coreProjection.Handle(
                new CoreProjectionProcessingMessage.CheckpointLoaded(
                    _projectionCorrelationId, checkpointTag, checkpointData));
            BeginLoadPrerecordedEvents(checkpointTag);
        }

        protected void SendPrerecordedEvent(
            EventStore.Core.Data.ResolvedEvent pair, CheckpointTag positionTag, long prerecordedEventMessageSequenceNumber)
        {
            var position = pair.OriginalEvent;
            var committedEvent = new ProjectionCoreServiceMessage.CommittedEventDistributed(
                Guid.Empty, default(EventPosition), position.EventStreamId, position.EventNumber,
                pair.Event.EventStreamId, pair.Event.EventNumber, pair.Link != null,
                ResolvedEvent.Create(
                    pair.Event.EventId, pair.Event.EventType, (pair.Event.Flags & PrepareFlags.IsJson) != 0,
                    pair.Event.Data, pair.Event.Metadata, pair.Event.TimeStamp), null, -1);
            _coreProjection.Handle(
                ProjectionSubscriptionMessage.CommittedEventReceived.FromCommittedEventDistributed(
                    committedEvent, positionTag, null, Guid.Empty, prerecordedEventMessageSequenceNumber));
        }

        protected abstract void BeginLoadPrerecordedEvents(CheckpointTag checkpointTag);

        protected void RequestRestart(string reason)
        {
            _coreProjection.Handle(new CoreProjectionProcessingMessage.RestartRequested(reason));
        }

        protected abstract void BeforeBeginLoadState();
        protected abstract void RequestLoadState();

        protected abstract void BeginWriteCheckpoint(
            CheckpointTag requestedCheckpointPosition, string requestedCheckpointState);

        public void Handle(CoreProjectionProcessingMessage.ReadyForCheckpoint message)
        {
            // ignore any messages - typically when faulted
            if (_stopped)
                return;
            // ignore any messages from previous checkpoints probably before RestartRequested
            if (message.Sender != _closingCheckpoint)
                return;
            if (!_inCheckpoint)
                throw new InvalidOperationException();
            BeginWriteCheckpoint(_requestedCheckpointPosition, _requestedCheckpointState);
        }

        protected void CheckpointWritten()
        {
            Contract.Requires(_closingCheckpoint != null);
            _lastCompletedCheckpointPosition = _requestedCheckpointPosition;
            _closingCheckpoint.Dispose();
            _closingCheckpoint = null;
            if (!_stopping)
                // ignore any writes pending in the current checkpoint (this is not the best, but they will never hit the storage, so it is safe)
                _currentCheckpoint.Start();
            _inCheckpoint = false;

            ProcessCheckpoints();
            _coreProjection.Handle(
                new CoreProjectionProcessingMessage.CheckpointCompleted(_lastCompletedCheckpointPosition));
        }

        public void Handle(CoreProjectionProcessingMessage.RestartRequested message)
        {
            RequestRestart(message.Reason);
        }

        private void EmitStateUpdatedEventsIfAny(string partition, PartitionStateCache.State oldState, PartitionStateCache.State newState)
        {
            if (_emitStateUpdated && newState != null)
            {
                //TODO: move it our to writeoutput stage (currently in CheckpointManager, so it can decide if it needs to generate stateupdated)
                var stateUpdatedEvents = CreateStateUpdatedEvents(
                    _namingBuilder, _positionTagger.MakeZeroCheckpointTag(), partition, oldState, newState);
                if (stateUpdatedEvents != null)
                    EventsEmitted(stateUpdatedEvents.ToArray());
            }
        }

        private static List<EmittedEvent> CreateStateUpdatedEvents(ProjectionNamesBuilder projectionNamesBuilder, CheckpointTag zeroCheckpointTag, string partition, PartitionStateCache.State oldState, PartitionStateCache.State newState)
        {
            var result = new List<EmittedEvent>();
            if (!String.IsNullOrEmpty(partition)
                && (oldState.CausedBy == null || oldState.CausedBy == zeroCheckpointTag))
            {
                result.Add(
                    new EmittedEvent(
                        projectionNamesBuilder.GetPartitionCatalogStreamName(), Guid.NewGuid(), "PartitionCreated", partition, newState.CausedBy,
                        null));
            }
            result.Add(
                new EmittedEvent(
                    projectionNamesBuilder.MakePartitionStateStreamName(partition), Guid.NewGuid(), "StateUpdated", newState.Data,
                    newState.CausedBy, oldState.CausedBy));
            return result;
        }


        public abstract void RecordEventOrder(ProjectionSubscriptionMessage.CommittedEventReceived message, Action committed);

        public void BeginLoadPartitionStateAt(string statePartition,
            CheckpointTag requestedStateCheckpointTag, Action<PartitionStateCache.State> loadCompleted)
        {
            string stateEventType;
            string partitionStateStreamName;
            if (_emitPartitionCheckpoints)
            {
                stateEventType = "Checkpoint";
                partitionStateStreamName = _namingBuilder.MakePartitionCheckpointStreamName(statePartition);
            }
            else
            {
                stateEventType = "StateUpdated";
                partitionStateStreamName = _namingBuilder.MakePartitionStateStreamName(statePartition);
            }
            _readRequestsInProgress++;
            var requestId =
                _readDispatcher.Publish(
                    new ClientMessage.ReadStreamEventsBackward(
                        Guid.NewGuid(), _readDispatcher.Envelope, partitionStateStreamName, -1, 1, resolveLinks: false, validationStreamVersion: null),
                    m =>
                    OnLoadPartitionStateReadStreamEventsBackwardCompleted(
                        m, requestedStateCheckpointTag, loadCompleted,
                        partitionStateStreamName, stateEventType));
            if (requestId != Guid.Empty)
                _loadStateRequests.Add(requestId);
        }

        private void OnLoadPartitionStateReadStreamEventsBackwardCompleted(
            ClientMessage.ReadStreamEventsBackwardCompleted message, CheckpointTag requestedStateCheckpointTag,
            Action<PartitionStateCache.State> loadCompleted, string partitionStreamName, string stateEventType)
        {
            //NOTE: the following remove may do nothing in tests as completed is raised before we return from publish. 
            _loadStateRequests.Remove(message.CorrelationId);

            _readRequestsInProgress--;
            if (message.Events.Length == 1)
            {
                EventRecord @event = message.Events[0].Event;
                if (@event.EventType == stateEventType)
                {
                    var loadedStateCheckpointTag = @event.Metadata.ParseJson<CheckpointTag>();
                    // always recovery mode? skip until state before current event
                    //TODO: skip event processing in case we know i has been already processed
                    if (loadedStateCheckpointTag < requestedStateCheckpointTag)
                    {
                        var state = new PartitionStateCache.State(Encoding.UTF8.GetString(@event.Data), loadedStateCheckpointTag);
                        loadCompleted(state);
                        return;
                    }
                }
            }
            if (message.NextEventNumber == -1)
            {
                var state = new PartitionStateCache.State("", _positionTagger.MakeZeroCheckpointTag());
                loadCompleted(state);
                return;
            }
            _readRequestsInProgress++;
            var requestId =
                _readDispatcher.Publish(
                    new ClientMessage.ReadStreamEventsBackward(
                        Guid.NewGuid(), _readDispatcher.Envelope, partitionStreamName, message.NextEventNumber, 1,
                        resolveLinks: false, validationStreamVersion: null),
                    m =>
                    OnLoadPartitionStateReadStreamEventsBackwardCompleted(m, requestedStateCheckpointTag, loadCompleted, partitionStreamName, stateEventType));
            if (requestId != Guid.Empty)
                _loadStateRequests.Add(requestId);
        }


    }
}
