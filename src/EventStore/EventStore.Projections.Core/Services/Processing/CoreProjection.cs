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
using System.Globalization;
using System.Linq;
using System.Text;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Utils;

namespace EventStore.Projections.Core.Services.Processing
{
    //TODO: replace Console.WriteLine with logging
    //TODO: separate check-pointing from projection handling
    public class CoreProjection : IDisposable,
                                  ICoreProjection,
                                  IHandle<ClientMessage.ReadEventsBackwardsCompleted>,
                                  IHandle<ClientMessage.WriteEventsCompleted>,
                                  IHandle<ProjectionMessage.Projections.CommittedEventReceived>,
                                  IHandle<ProjectionMessage.Projections.CheckpointSuggested>,
                                  IHandle<ProjectionMessage.Projections.ReadyForCheckpoint>
    {
        private const string ProjectionsStreamPrefix = "$projections-";
        private const string ProjectionsStateStreamSuffix = "-state";
        private const string ProjectionCheckpointStreamSuffix = "-checkpoint";

        [Flags]
        internal enum State : uint
        {
            Initial = 0x80000000,
            LoadStateRequsted = 0x1,
            StateLoaded = 0x2,
            Subscribed = 0x4,
            Running = 0x08,
            Paused = 0x10,
            Resumed = 0x20,
            Stopping = 0x40,
            Stopped = 0x80,
            FaultedStopping = 0x100,
            Faulted = 0x200,
        }

        private readonly string _name;

        private readonly IPublisher _publisher;
        private readonly IProjectionStateHandler _projectionStateHandler;
        private readonly string _projectionCheckpointStreamId;

        private readonly Guid _projectionCorrelationId;
        internal readonly ProjectionConfig _projectionConfig;
        private readonly ILogger _logger;

        private State _state;
        private bool _recoveryMode;

        //NOTE: this queue hides the real length of projection stage incoming queue, so the almost empty stage queue may still handle many long projection queues

        private int _nextStateIndexToRequest;

        private ProjectionCheckpoint _currentCheckpoint;
        private ProjectionCheckpoint _closingCheckpoint;
        //NOTE: this may note work well on recovery when reading from any index instead of replying all the event stream (index will likely render less events than original event stream)
        private int _handledEventsAfterCheckpoint;

        private CheckpointTag _requestedCheckpointPosition;

        //TODO: join incheckpoint fields into single checkpoint state field
        private bool _inCheckpoint;
        private int _inCheckpointWriteAttempt;
        private readonly PositionTracker _lastProcessedEventPosition;
        private int _lastWrittenCheckpointEventNumber;
        private string _requestedCheckpointStateJson;

        private CheckpointTag _lastCompletedCheckpointPosition;
        private int _eventsProcessedAfterRestart;
        internal string _faultedReason;
        private readonly EventFilter _eventFilter;
        internal readonly CheckpointStrategy _checkpointStrategy;
        private Event _checkpointEventToBePublished;

        private readonly
            RequestResponseDispatcher<ClientMessage.ReadEventsBackwards, ClientMessage.ReadEventsBackwardsCompleted>
            _readDispatcher;

        private string _handlerPartition;
        internal bool _subscriptionPaused;
        private bool _tickPending;
        private readonly PartitionStateCache _partitionStateCache;


        public CoreProjection(
            string name, Guid projectionCorrelationId, IPublisher publisher,
            IProjectionStateHandler projectionStateHandler, ProjectionConfig projectionConfig, ILogger logger = null)
        {
            if (name == null) throw new ArgumentNullException("name");
            if (name == "") throw new ArgumentException("name");
            if (publisher == null) throw new ArgumentNullException("publisher");
            if (projectionStateHandler == null) throw new ArgumentNullException("projectionStateHandler");
            _projectionCorrelationId = projectionCorrelationId;
            _name = name;
            _projectionConfig = projectionConfig;
            _logger = logger;
            _publisher = publisher;
            _projectionStateHandler = projectionStateHandler;
            _readDispatcher =
                new RequestResponseDispatcher
                    <ClientMessage.ReadEventsBackwards, ClientMessage.ReadEventsBackwardsCompleted>(
                    _publisher, v => v.CorrelationId, v => v.CorrelationId);
            _projectionCheckpointStreamId = ProjectionsStreamPrefix + _name + ProjectionCheckpointStreamSuffix;
            var builder = new CheckpointStrategy.Builder();
            _projectionStateHandler.ConfigureSourceProcessingStrategy(builder);
            _checkpointStrategy = builder.Build(_projectionConfig.Mode);
            _eventFilter = _checkpointStrategy.EventFilter;
            _lastProcessedEventPosition = new PositionTracker(_checkpointStrategy.PositionTagger);
            _partitionStateCache = new PartitionStateCache();
            _coreProjectionQueue = new CoreProjectionQueue(this);
            GoToState(State.Initial);
        }

        public CoreProjectionQueue CoreProjectionQueue
        {
            get { return _coreProjectionQueue; }
        }

        public void Start()
        {
            EnsureState(State.Initial);
            GoToState(State.LoadStateRequsted);
        }

        public void Stop()
        {
            EnsureState(State.Subscribed | State.Paused | State.Resumed | State.Running);
            GoToState(State.Stopping);
        }

        public string GetProjectionState()
        {
            //TODO: separate requesting valid only state (not catching-up, non-stopped etc)
            //EnsureState(State.StateLoaded | State.Stopping | State.Subscribed | State.Paused | State.Resumed | State.Running);
            return _partitionStateCache.GetLockedPartitionState("");
        }

        public ProjectionStatistics GetStatistics()
        {
            return new ProjectionStatistics
                {
                    Mode = _projectionConfig.Mode,
                    Name = _name,
                    Position = _lastProcessedEventPosition.LastTag,
                    StateReason = "",
                    Status =
                        _state.EnumVaueName() + (_recoveryMode ? "/Recovery" : "")
                        + (_subscriptionPaused && _state != State.Paused ? "/Subscription Paused" : ""),
                    LastCheckpoint =
                        string.Format(CultureInfo.InvariantCulture, "{0}", _lastCompletedCheckpointPosition),
                    EventsProcessedAfterRestart = _eventsProcessedAfterRestart,
                    BufferedEvents = CoreProjectionQueue.GetBufferedEventCount(),
                    WritePendingEventsBeforeCheckpoint =
                        _closingCheckpoint != null ? _closingCheckpoint.GetWritePendingEvents() : 0,
                    WritePendingEventsAfterCheckpoint =
                        _currentCheckpoint != null ? _currentCheckpoint.GetWritePendingEvents() : 0,
                    ReadsInProgress =
                        _readDispatcher.ActiveRequestCount
                        + (_closingCheckpoint != null ? _closingCheckpoint.GetReadsInProgress() : 0)
                        + (_currentCheckpoint != null ? _currentCheckpoint.GetReadsInProgress() : 0),
                    WritesInProgress =
                        ((_inCheckpointWriteAttempt != 0) ? 1 : 0)
                        + (_closingCheckpoint != null ? _closingCheckpoint.GetWritesInProgress() : 0)
                        + (_currentCheckpoint != null ? _currentCheckpoint.GetWritesInProgress() : 0),
                    PartitionsCached = _partitionStateCache.CachedItemCount,
                    CheckpointStatus =
                        _inCheckpointWriteAttempt > 0
                            ? "Writing (" + _inCheckpointWriteAttempt + ")"
                            : (_inCheckpoint ? "Requested" : ""),
                };
        }

#if PROJECTIONS_ASSERTS
        //NOTE: this is temporary dictionary to check internal consistency.  Must be removed
        //TODO: remove
        private readonly Dictionary<string, int> _lastSequencesByPartition = new Dictionary<string, int>();
        private readonly CoreProjectionQueue _coreProjectionQueue;
#endif

        public void Handle(ProjectionMessage.Projections.CommittedEventReceived message)
        {
            EnsureState(CoreProjection.State.Running | CoreProjection.State.Paused | CoreProjection.State.Stopping | CoreProjection.State.Stopped | CoreProjection.State.FaultedStopping | CoreProjection.State.Faulted);
            this.CoreProjectionQueue.HandleCommittedEventReceived(this, message);
        }

        public void Handle(ProjectionMessage.Projections.CheckpointSuggested message)
        {
            EnsureState(CoreProjection.State.Running | CoreProjection.State.Paused | CoreProjection.State.Stopping | CoreProjection.State.Stopped | CoreProjection.State.FaultedStopping | CoreProjection.State.Faulted);
            this.CoreProjectionQueue.HandleCheckpointSuggested(this, message);
        }

        // ready for checkpoint is sent directly to the core projection by a checkpoint object 
        public void Handle(ProjectionMessage.Projections.ReadyForCheckpoint message)
        {
            EnsureState(State.Running | State.Paused | State.Stopping | State.FaultedStopping);
            if (!_inCheckpoint)
                throw new InvalidOperationException();
            // all emitted events caused by events before the checkpoint position have been written  
            // unlock states, so the cache can be clean up as they can now be safely reloaded from the ES
            _partitionStateCache.Unlock(_requestedCheckpointPosition);
            _inCheckpointWriteAttempt = 1;
            //TODO: pass correct expected version
            _checkpointEventToBePublished = new Event(
                Guid.NewGuid(), "ProjectionCheckpoint", false,
                _requestedCheckpointStateJson == null ? null : Encoding.UTF8.GetBytes(_requestedCheckpointStateJson),
                _requestedCheckpointPosition.ToJsonBytes());
            PublishWriteCheckpointEvent();
        }

        public void Handle(ClientMessage.WriteEventsCompleted message)
        {
            EnsureState(State.Running | State.Paused | State.Stopping | State.FaultedStopping);
            if (!_inCheckpoint || _inCheckpointWriteAttempt == 0)
                throw new InvalidOperationException();
            if (message.ErrorCode == OperationErrorCode.Success)
            {
                if (_logger != null)
                    _logger.Trace(
                        "Checkpoint has be written for projection {0} at sequence number {1} (current)", _name,
                        message.EventNumber);
                _lastCompletedCheckpointPosition = _requestedCheckpointPosition;
                _lastWrittenCheckpointEventNumber = message.EventNumber
                                                    +
                                                    (_lastWrittenCheckpointEventNumber == ExpectedVersion.NoStream // account for StreamCreated
                                                         ? 1
                                                         : 0);

                _closingCheckpoint = null;
                if (_state != State.Stopping && _state != State.FaultedStopping)
                    // ignore any writes pending in the current checkpoint (this is not the best, but they will never hit the storage, so it is safe)
                    _currentCheckpoint.Start();
                _inCheckpoint = false;
                _inCheckpointWriteAttempt = 0;

                if (_state != State.Running)
                {
                    ProcessCheckpoints();
                    if (_state == State.Paused)
                    {
                        TryResume();
                    }
                    else if (_state == State.Stopping)
                        GoToState(State.Stopped);
                    else if (_state == State.FaultedStopping)
                        GoToState(State.Faulted);
                }
            }
            else
            {
                if (_logger != null)
                    _logger.Info(
                        "Failed to write projection checkpoint to stream {0}. Error: {1}", message.EventStreamId,
                        Enum.GetName(typeof (OperationErrorCode), message.ErrorCode));
                if (message.ErrorCode == OperationErrorCode.CommitTimeout
                    || message.ErrorCode == OperationErrorCode.ForwardTimeout
                    || message.ErrorCode == OperationErrorCode.PrepareTimeout
                    || message.ErrorCode == OperationErrorCode.WrongExpectedVersion)
                {
                    if (_logger != null) _logger.Info("Retrying write checkpoint to {0}", message.EventStreamId);
                    _inCheckpointWriteAttempt++;
                    PublishWriteCheckpointEvent();
                }
                else
                    throw new NotSupportedException("Unsupported error code received");
            }
        }

        public void Handle(ClientMessage.ReadEventsBackwardsCompleted message)
        {
            _readDispatcher.Handle(message);
        }

        internal void GoToState(State state)
        {
            _state = state; // set state before transition to allow further state change
            switch (state)
            {
                case State.Running:
                    _coreProjectionQueue.SetRunning();
                    break;
                case State.Paused:
                    _coreProjectionQueue.SetPaused();
                    break;
                default:
                    _coreProjectionQueue.SetStopped();
                    break;
            }
            switch (state)
            {
                case State.Initial:
                    EnterInitial();
                    break;
                case State.LoadStateRequsted:
                    EnterLoadStateRequested();
                    break;
                case State.StateLoaded:
                    EnterStateLoaded();
                    break;
                case State.Subscribed:
                    EnterSubscribed();
                    break;
                case State.Running:
                    EnterRunning();
                    break;
                case State.Paused:
                    EnterPaused();
                    break;
                case State.Resumed:
                    EnterResumed();
                    break;
                case State.Stopping:
                    EnterStopping();
                    break;
                case State.Stopped:
                    EnterStopped();
                    break;
                case State.FaultedStopping:
                    EnterFaultedStopping();
                    break;
                case State.Faulted:
                    EnterFaulted();
                    break;
                default:
                    throw new Exception();
            }
        }

        private void EnterInitial()
        {
            _recoveryMode = true;
            // We don't know anything about the projection, whether it has emitted any events.  
            // Thus we are in recovery until first state snapshot
            // This has not impact on non-emitting projections and it will not impact significantly
            // new emitting projections.  AdHoc projections will run in recovery forever.
            _nextStateIndexToRequest = -1; // from the end
            _partitionStateCache.CacheAndLockPartitionState("", "", null);
            // NOTE: this is to workaround exception in GetState requests submitted by client
        }

        private void EnterLoadStateRequested()
        {
            if (_projectionConfig.CheckpointsEnabled)
            {
                const int recordsToRequest = 10;
                _readDispatcher.Publish(
                    new ClientMessage.ReadEventsBackwards(
                        Guid.NewGuid(), new SendToThisEnvelope(this), _projectionCheckpointStreamId,
                        _nextStateIndexToRequest, recordsToRequest, resolveLinks: false), OnLoadStateCompleted);
            }
            else
                InitializeNewProjection();
        }

        private void EnterStateLoaded()
        {
            GoToState(State.Subscribed);
        }

        private void EnterSubscribed()
        {
            CoreProjectionQueue.InitializeQueue(this);
            _requestedCheckpointPosition = null;
            _currentCheckpoint = new ProjectionCheckpoint(
                _publisher, this, _recoveryMode, _lastProcessedEventPosition.LastTag,
                _projectionConfig.MaxWriteBatchLength, _logger);
            _currentCheckpoint.Start();
            _publisher.Publish(
                new ProjectionMessage.Projections.SubscribeProjection(
                    _projectionCorrelationId, this, _lastProcessedEventPosition.LastTag, _checkpointStrategy,
                    _projectionConfig.CheckpointUnhandledBytesThreshold));
            _publisher.Publish(new ProjectionMessage.Projections.Started(_projectionCorrelationId));
            GoToState(State.Running);
        }

        private void EnterRunning()
        {
            this.CoreProjectionQueue.ProcessEvent();
        }

        private void EnterPaused()
        {
            PauseSubscription();
        }

        internal void PauseSubscription()
        {
            if (!_subscriptionPaused)
            {
                _subscriptionPaused = true;
                _publisher.Publish(
                    new ProjectionMessage.Projections.PauseProjectionSubscription(_projectionCorrelationId));
            }
        }

        private void EnterResumed()
        {
            ResumeSubscription();
            GoToState(State.Running);
        }

        internal void ResumeSubscription()
        {
            if (_subscriptionPaused && _state == State.Running)
            {
                _subscriptionPaused = false;
                _publisher.Publish(
                    new ProjectionMessage.Projections.ResumeProjectionSubscription(_projectionCorrelationId));
            }
        }

        private void EnterStopping()
        {
            Console.WriteLine("Stopping");
            _publisher.Publish(new ProjectionMessage.Projections.UnsubscribeProjection(_projectionCorrelationId));
            // core projection may be stopped to change its configuration
            // it is important to checkpoint it so no writes pending remain when stopped
            if (RequestCheckpointToStop())
                return;
            GoToState(State.Stopped);
        }

        private void EnterStopped()
        {
            _publisher.Publish(new ProjectionMessage.Projections.Stopped(_projectionCorrelationId));
            Console.WriteLine("Stopped");
        }

        private void EnterFaultedStopping()
        {
            _publisher.Publish(new ProjectionMessage.Projections.UnsubscribeProjection(_projectionCorrelationId));
            // checkpoint last known correct state on fault
            if (RequestCheckpointToStop())
                return;
            GoToState(State.Faulted);
        }

        private void EnterFaulted()
        {
            _publisher.Publish(new ProjectionMessage.Projections.Faulted(_projectionCorrelationId, _faultedReason));
        }

        private void LoadProjectionState(string newState)
        {
            EnsureState(State.Initial | State.LoadStateRequsted);
            _partitionStateCache.CacheAndLockPartitionState("", newState, null);
            try
            {
                SetHandlerState("");
                GoToState(State.StateLoaded);
            }
            catch (Exception ex)
            {
                LoadProjectionStateFaulted(newState, ex);
            }
        }

        private void SetHandlerState(string partition)
        {
            if (_handlerPartition == partition)
                return;
            string newState = _partitionStateCache.GetLockedPartitionState(partition);
            _handlerPartition = partition;
            if (!string.IsNullOrEmpty(newState))
                _projectionStateHandler.Load(newState);
            else
                _projectionStateHandler.Initialize();
        }

        private void LoadProjectionStateFaulted(string newState, Exception ex)
        {
            _faultedReason =
                string.Format(
                    "Cannot load the {0} projection state.\r\nHandler: {1}\r\nState:\r\n\r\n{2}\r\n\r\nMessage:\r\n\r\n{3}",
                    _name, GetHandlerTypeName(), newState, ex.Message);
            if (_logger != null)
                _logger.ErrorException(ex, _faultedReason);
            GoToState(State.Faulted);
        }

        private string GetHandlerTypeName()
        {
            return _projectionStateHandler.GetType().Namespace + "." + _projectionStateHandler.GetType().Name;
        }

        private void TryResume()
        {
            GoToState(State.Resumed);
        }

        internal void EnsureTickPending()
        {
            if (_tickPending)
                return;
            if (_state == State.Paused)
                return;
            _tickPending = true;
            _publisher.Publish(new ProjectionMessage.CoreService.Tick(Tick));
        }

        private void ProcessCheckpointSuggestedEvent(
            ProjectionMessage.Projections.CheckpointSuggested checkpointSuggested)
        {
            _lastProcessedEventPosition.UpdateByCheckpointTagForward(checkpointSuggested.CheckpointTag);
            RequestCheckpoint();
        }

        private void ProcessCommittedEvent(CoreProjection.CommittedEventWorkItem committedEventWorkItem, ProjectionMessage.Projections.CommittedEventReceived message,
            string partition)
        {
            if (message.Data == null)
                throw new NotSupportedException();

            EnsureState(State.Running);
            InternalProcessCommittedEvent(committedEventWorkItem, partition, message);
        }

        private void InternalProcessCommittedEvent(CoreProjection.CommittedEventWorkItem committedEventWorkItem, string partition,
            ProjectionMessage.Projections.CommittedEventReceived message)
        {
            string newState = null;
            EmittedEvent[] emittedEvents = null;

            //TODO: not emitting (optimized) projection handlers can skip serializing state on each processed event
            bool hasBeenProcessed;
            try
            {
                bool passedFilter = EventPassesFilter(message);
                hasBeenProcessed = passedFilter
                                   && ProcessEventByHandler(partition, message, out newState, out emittedEvents);
            }
            catch (Exception ex)
            {
                ProcessEventFaulted(string.Format(
                    "The {0} projection failed to process an event.\r\nHandler: {1}\r\nEvent Position: {2}\r\n\r\nMessage:\r\n\r\n{3}",
                    _name, GetHandlerTypeName(), message.Position, ex.Message), ex);
                newState = null;
                emittedEvents = null;
                hasBeenProcessed = false;
            }
            newState = newState ?? "";
            if (hasBeenProcessed)
            {
                if (!ProcessEmittedEvents(committedEventWorkItem, emittedEvents))
                    return;

                if (_partitionStateCache.GetLockedPartitionState(partition) != newState)
                    // ensure state actually changed
                {
                    var lockPartitionStateAt = partition != ""
                                                   ? _checkpointStrategy.PositionTagger.MakeCheckpointTag(message)
                                                   : null;
                    _partitionStateCache.CacheAndLockPartitionState(partition, newState, lockPartitionStateAt);
                    if (_projectionConfig.PublishStateUpdates)
                        EmitStateUpdated(committedEventWorkItem, partition, newState);
                }
            }
        }

        private bool ProcessEmittedEvents(CoreProjection.CommittedEventWorkItem committedEventWorkItem, EmittedEvent[] emittedEvents)
        {
            if (_projectionConfig.EmitEventEnabled)
                EmitEmittedEvents(committedEventWorkItem, emittedEvents);
            else if (emittedEvents != null && emittedEvents.Length > 0)
            {
                ProcessEventFaulted("emit_event is not enabled by the projection configuration/mode");
                return false;
            }
            return true;
        }

        private void FinilizeCommittedEventProcessing(ProjectionMessage.Projections.CommittedEventReceived message)
        {
            if (message.Data == null)
                throw new NotSupportedException();
            if (_state == State.Faulted || _state == State.FaultedStopping)
                return;
            EnsureState(State.Running);
            //TODO: move to separate projection method and cache result in work item
            bool passedFilter = EventPassesFilter(message);
            if (passedFilter)
            {
                _lastProcessedEventPosition.Update(message);
                _eventsProcessedAfterRestart++;
            }
            else
            {
                if (EventPassesSourceFilter(message))
                    _lastProcessedEventPosition.Update(message);
                else
                    _lastProcessedEventPosition.UpdatePosition(message.Position);
            }
        }

        private void SubmitScheduledWritesAndProcessCheckpoints(List<EmittedEvent[]> scheduledWrites)
        {
            if (_state == State.Faulted)
                return;
            EnsureState(State.Running);
            if (scheduledWrites != null)
                foreach (var scheduledWrite in scheduledWrites)
                {
                    _currentCheckpoint.EmitEvents(scheduledWrite, _lastProcessedEventPosition.LastTag);
                }
            _handledEventsAfterCheckpoint++;
            if (_state != State.Faulted && _state != State.FaultedStopping)
                ProcessCheckpoints();
        }

        private bool ProcessEventByHandler(
            string partition, ProjectionMessage.Projections.CommittedEventReceived message, out string newState,
            out EmittedEvent[] emittedEvents)
        {
            SetHandlerState(partition);
            return _projectionStateHandler.ProcessEvent(
                message.Position, message.EventStreamId, message.Data.EventType,
                _eventFilter.GetCategory(message.PositionStreamId), message.Data.EventId, message.EventSequenceNumber,
                Encoding.UTF8.GetString(message.Data.Metadata), Encoding.UTF8.GetString(message.Data.Data), out newState,
                out emittedEvents);
        }

        private bool EventPassesFilter(ProjectionMessage.Projections.CommittedEventReceived message)
        {
            //TODO: support category
            return _eventFilter.Passes(message.ResolvedLinkTo, message.PositionStreamId, message.Data.EventType);
        }

        private bool EventPassesSourceFilter(ProjectionMessage.Projections.CommittedEventReceived message)
        {
            //TODO: support category
            return _eventFilter.PassesSource(message.ResolvedLinkTo, message.PositionStreamId);
        }

        private void EmitEmittedEvents(CoreProjection.CommittedEventWorkItem committedEventWorkItem, EmittedEvent[] emittedEvents)
        {
            bool result = emittedEvents != null && emittedEvents.Length > 0;
            if (result)
                committedEventWorkItem.ScheduleEmitEvents(emittedEvents);
        }

        private void EmitStateUpdated(CoreProjection.CommittedEventWorkItem committedEventWorkItem, string partition, string newState)
        {
            committedEventWorkItem.ScheduleEmitEvents(
                new[]
                    {
                        new EmittedEvent(MakePartitionStateStreamName(partition), Guid.NewGuid(), "StateUpdated", newState)
                    });
        }

        private void ProcessEventFaulted(string faultedReason, Exception ex = null)
        {
            _faultedReason = faultedReason;
            if (_logger != null)
            {
                if (ex != null)
                    _logger.ErrorException(ex, _faultedReason);
                else
                    _logger.Error(_faultedReason);
            }
            GoToState(State.FaultedStopping);
        }

        private void ProcessCheckpoints()
        {
            EnsureState(State.Running | State.Paused | State.Stopping | State.FaultedStopping);
            if (_projectionConfig.CheckpointsEnabled)
                if (_handledEventsAfterCheckpoint >= _projectionConfig.CheckpointHandledThreshold)
                    RequestCheckpoint();
                else
                {
                    // TODO: projections emitting events without checkpoints will eat memory by creating new emitted streams  
                }
        }

        private bool RequestCheckpointToStop()
        {
            if (_projectionConfig.CheckpointsEnabled)
                // do not request checkpoint if no events were processed since last checkpoint
                if (_lastCompletedCheckpointPosition < _lastProcessedEventPosition.LastTag)
                {
                    RequestCheckpoint();
                    return true;
                }
            return false;
        }

        private void RequestCheckpoint()
        {
            if (!_projectionConfig.CheckpointsEnabled)
                throw new InvalidOperationException("Checkpoints are not allowed");
            if (!_inCheckpoint)
                CompleteCheckpoint();
            else if (_state != State.Stopping && _state != State.FaultedStopping)
                // stopping projection is already paused
                GoToState(State.Paused);
        }

        private void CompleteCheckpoint()
        {
            CheckpointTag requestedCheckpointPosition = _lastProcessedEventPosition.LastTag;
            if (requestedCheckpointPosition == _lastCompletedCheckpointPosition)
                return; // either suggested or requested to stop
            _inCheckpoint = true;
            _requestedCheckpointPosition = requestedCheckpointPosition;
            _requestedCheckpointStateJson = GetProjectionState();
            _handledEventsAfterCheckpoint = 0;

            _closingCheckpoint = _currentCheckpoint;
            _recoveryMode = false;
            _currentCheckpoint = new ProjectionCheckpoint(
                _publisher, this, false, requestedCheckpointPosition, _projectionConfig.MaxWriteBatchLength, _logger);
            // checkpoint only after assigning new current checkpoint, as it may call back immediately
            _closingCheckpoint.Prepare(requestedCheckpointPosition);
        }

        private void EnsureState(State expectedStates)
        {
            if ((_state & expectedStates) == 0)
            {
                throw new Exception(
                    string.Format("Current state is {0}. Expected states are: {1}", _state, expectedStates));
            }
        }

        private void Tick()
        {
            _tickPending = false;
            EnsureState(State.Running | State.Paused | State.Stopping | State.FaultedStopping | State.Faulted);
            // we may get into faulted any time, so it is allowed
            if (_state == State.Running)
                GoToState(State.Running);
        }

        private void PublishWriteCheckpointEvent()
        {
            if (_logger != null)
                _logger.Trace(
                    "Writing checkpoint for {0} at {1} with expected version number {2}", _name,
                    _requestedCheckpointPosition, _lastWrittenCheckpointEventNumber);
            _publisher.Publish(
                new ClientMessage.WriteEvents(
                    Guid.NewGuid(), new SendToThisEnvelope(this), _projectionCheckpointStreamId,
                    _lastWrittenCheckpointEventNumber, _checkpointEventToBePublished));
        }

        private void OnLoadStateCompleted(ClientMessage.ReadEventsBackwardsCompleted message)
        {
            EnsureState(State.LoadStateRequsted);
            if (message.Events.Length > 0)
            {
                EventRecord checkpoint = message.Events.FirstOrDefault(v => v.EventType == "ProjectionCheckpoint");
                if (checkpoint != null)
                {
                    InitializeProjectionFromCheckpoint(checkpoint);
                    return;
                }
            }

            if (message.NextEventNumber == -1)
                InitializeNewProjection();
            else
            {
                _nextStateIndexToRequest = message.NextEventNumber;
                GoToState(State.LoadStateRequsted);
            }
        }

        private void InitializeNewProjection()
        {
            _lastProcessedEventPosition.UpdateToZero();
            _lastCompletedCheckpointPosition = _lastProcessedEventPosition.LastTag;
            _lastWrittenCheckpointEventNumber = ExpectedVersion.NoStream;
            LoadProjectionState("");
        }

        private void InitializeProjectionFromCheckpoint(EventRecord checkpoint)
        {
//TODO: handle errors
            var checkpointTag = checkpoint.Metadata.ParseJson<CheckpointTag>();
            _lastProcessedEventPosition.UpdateByCheckpointTag(checkpointTag);
            _lastCompletedCheckpointPosition = checkpointTag;
            _lastWrittenCheckpointEventNumber = checkpoint.EventNumber;

            string newState = Encoding.UTF8.GetString(checkpoint.Data);

            //TODO: initialize projection state here (test it)
            //TODO: write test to ensure projection state is correctly loaded from a checkpoint and posted back when enough empty records processed
            LoadProjectionState(newState);
        }

        private void BeginStatePartitionLoad(
            ProjectionMessage.Projections.CommittedEventReceived @event, Action loadCompleted)
        {
            string statePartition = _checkpointStrategy.StatePartitionSelector.GetStatePartition(@event);
            if (statePartition == "") // root is always cached
            {
                loadCompleted();
                return;
            }
            string state = _partitionStateCache.TryGetAndLockPartitionState(
                statePartition, _checkpointStrategy.PositionTagger.MakeCheckpointTag(@event));
            if (state != null)
                loadCompleted();
            else
            {
                string partitionStateStreamName = MakePartitionStateStreamName(statePartition);
                _readDispatcher.Publish(
                    new ClientMessage.ReadEventsBackwards(
                        Guid.NewGuid(), new SendToThisEnvelope(this), partitionStateStreamName, -1, 1,
                        resolveLinks: false),
                    m => OnLoadStatePartitionCompleted(statePartition, @event, m, loadCompleted));
            }
        }

        private string MakePartitionStateStreamName(string statePartition)
        {
            return ProjectionsStreamPrefix + _name + (string.IsNullOrEmpty(statePartition) ? "" : "-") + statePartition
                   + ProjectionsStateStreamSuffix;
        }

        private void OnLoadStatePartitionCompleted(
            string partition, ProjectionMessage.Projections.CommittedEventReceived committedEventReceived,
            ClientMessage.ReadEventsBackwardsCompleted message, Action loadCompleted)
        {
            var positionTag = _checkpointStrategy.PositionTagger.MakeCheckpointTag(committedEventReceived);
            if (message.Events.Length == 1)
            {
                EventRecord @event = message.Events[0];
                if (@event.EventType == "StateUpdated")
                {
                    var checkpointTag = @event.Metadata.ParseJson<CheckpointTag>();
                    // always recovery mode? skip until state before current event
                    //TODO: skip event processing in case we know i has been already processed
                    CheckpointTag eventPositionTag = positionTag;
                    if (checkpointTag < eventPositionTag)
                    {
                        _partitionStateCache.CacheAndLockPartitionState(
                            partition, Encoding.UTF8.GetString(@event.Data), eventPositionTag);
                        loadCompleted();
                        EnsureTickPending();
                        return;
                    }
                }
            }
            if (message.NextEventNumber == -1)
            {
                _partitionStateCache.CacheAndLockPartitionState(partition, "", positionTag);
                loadCompleted();
                EnsureTickPending();
                return;
            }
            string partitionStateStreamName = MakePartitionStateStreamName(partition);
            _readDispatcher.Publish(
                new ClientMessage.ReadEventsBackwards(
                    Guid.NewGuid(), new SendToThisEnvelope(this), partitionStateStreamName, message.NextEventNumber, 1,
                    resolveLinks: false),
                m => OnLoadStatePartitionCompleted(partition, committedEventReceived, m, loadCompleted));
        }

        public void Dispose()
        {
            if (_projectionStateHandler != null)
                _projectionStateHandler.Dispose();
        }

        internal abstract class WorkItem : StagedTask
        {
            private readonly int _lastStage;
            private Action<int> _complete;
            private int _onStage;

            public WorkItem(string stream)
                : base(stream)
            {
                _lastStage = 2;
            }

            public override void Process(int onStage, Action<int> readyForStage)
            {
                _complete = readyForStage;
                _onStage = onStage;
                switch (onStage)
                {
                    case 0:
                        Load();
                        break;
                    case 1:
                        ProcessEvent();
                        break;
                    case 2:
                        WriteOutput();
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }

            protected virtual void WriteOutput()
            {
                NextStage();
            }

            protected virtual void Load()
            {
                NextStage();
            }

            protected virtual void ProcessEvent()
            {
                NextStage();
            }

            protected void NextStage()
            {
                _complete(_onStage == _lastStage ? -1 : _onStage + 1);
            }
        }

        internal class CommittedEventWorkItem : CoreProjection.WorkItem
        {
            private readonly ProjectionMessage.Projections.CommittedEventReceived _message;
            private readonly string _partition;
            private readonly CoreProjection _projection;
            private readonly CheckpointTag _tag;
            private List<EmittedEvent[]> _scheduledWrites;

            public CommittedEventWorkItem(
                CoreProjection projection, ProjectionMessage.Projections.CommittedEventReceived message,
                string partition, CheckpointTag tag)
                : base(message.EventStreamId)
            {
                _projection = projection;
                _message = message;
                _partition = partition;
                _tag = tag;
            }

            protected override void Load()
            {
                _projection.BeginStatePartitionLoad(_message, LoadCompleted);
            }

            private void LoadCompleted()
            {
                NextStage();
            }

            protected override void ProcessEvent()
            {
                _projection.ProcessCommittedEvent(this, _message, _partition);
                NextStage();
            }

            protected override void WriteOutput()
            {
                _projection.FinilizeCommittedEventProcessing(_message);
                _projection.SubmitScheduledWritesAndProcessCheckpoints(_scheduledWrites);
                NextStage();
            }

            public void ScheduleEmitEvents(EmittedEvent[] emittedEvents)
            {
                if (_scheduledWrites == null)
                    _scheduledWrites = new List<EmittedEvent[]>();
                _scheduledWrites.Add(emittedEvents);
            }
        }

        internal class CheckpointSuggestedWorkItem : CoreProjection.WorkItem
        {
            private readonly ProjectionMessage.Projections.CheckpointSuggested _message;
            private readonly CoreProjection _projection;
            private readonly CheckpointTag _tag;

            public CheckpointSuggestedWorkItem(
                CoreProjection projection, ProjectionMessage.Projections.CheckpointSuggested message, CheckpointTag tag)
                : base("")
            {
                _projection = projection;
                _message = message;
                _tag = tag;
            }

            protected override void WriteOutput()
            {
                _projection.ProcessCheckpointSuggestedEvent(_message);
                NextStage();
            }
        }

        internal void SetFaulted(Exception ex)
        {
            _faultedReason = ex.Message;
            GoToState(CoreProjection.State.Faulted);
        }
    }
}
