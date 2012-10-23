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
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public abstract class CoreProjectionCheckpointManager : IHandle<ProjectionMessage.Projections.ReadyForCheckpoint>
    {
        private readonly IPublisher _publisher;

        protected readonly
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>
            _readDispatcher;

        protected readonly RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>
            _writeDispatcher;

        protected readonly string _name;
        protected readonly ILogger _logger;

        private readonly ICoreProjection _coreProjection;
        private readonly Guid _projectionCorrelationId;
        private readonly ProjectionConfig _projectionConfig;


        private ProjectionCheckpoint _currentCheckpoint;
        private ProjectionCheckpoint _closingCheckpoint;
        private int _handledEventsAfterCheckpoint;
        private CheckpointTag _requestedCheckpointPosition;
        private bool _inCheckpoint;
        protected int _inCheckpointWriteAttempt;
        private string _requestedCheckpointState;
        private CheckpointTag _lastCompletedCheckpointPosition;
        private readonly PositionTracker _lastProcessedEventPosition;
        private float _lastProcessedEventProgress;

        private int _eventsProcessedAfterRestart;
        private bool _stateLoaded;
        private bool _started;
        private bool _stopping;
        private bool _stateRequested;
        private string _currentProjectionState;

        protected CoreProjectionCheckpointManager(
            ICoreProjection coreProjection, IPublisher publisher, Guid projectionCorrelationId,
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted> readDispatcher,
            RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted> writeDispatcher,
            ProjectionConfig projectionConfig, string projectionCheckpointStreamId, string name,
            PositionTagger positionTagger)
        {
            if (coreProjection == null) throw new ArgumentNullException("coreProjection");
            if (publisher == null) throw new ArgumentNullException("publisher");
            if (readDispatcher == null) throw new ArgumentNullException("readDispatcher");
            if (writeDispatcher == null) throw new ArgumentNullException("writeDispatcher");
            if (projectionConfig == null) throw new ArgumentNullException("projectionConfig");
            if (projectionCheckpointStreamId == null) throw new ArgumentNullException("projectionCheckpointStreamId");
            if (projectionCheckpointStreamId == "") throw new ArgumentException("projectionCheckpointStreamId");
            if (name == null) throw new ArgumentNullException("name");
            if (positionTagger == null) throw new ArgumentNullException("positionTagger");
            if (name == "") throw new ArgumentException("name");
            _lastProcessedEventPosition = new PositionTracker(positionTagger);
            _coreProjection = coreProjection;
            _publisher = publisher;
            _projectionCorrelationId = projectionCorrelationId;
            _readDispatcher = readDispatcher;
            _writeDispatcher = writeDispatcher;
            _projectionConfig = projectionConfig;
            _logger = LogManager.GetLoggerFor<CoreProjectionCheckpointManager>();
            _name = name;
        }

        public void Start(CheckpointTag checkpointTag)
        {
            if (!_stateLoaded)
                throw new InvalidOperationException("State is not loaded");
            if (_started)
                throw new InvalidOperationException("Already started");
            _started = true;
            _lastProcessedEventPosition.UpdateByCheckpointTagInitial(checkpointTag);
            _lastProcessedEventProgress = -1;
            _lastCompletedCheckpointPosition = checkpointTag;
            _requestedCheckpointPosition = null;
            _currentCheckpoint = new ProjectionCheckpoint(
                _publisher, this, _lastProcessedEventPosition.LastTag, _projectionConfig.MaxWriteBatchLength, _logger);
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
        }

        public ProjectionStatistics GetStatistics()
        {
            return new ProjectionStatistics
                {
                    Mode = _projectionConfig.Mode,
                    Name = null,
                    Position = _lastProcessedEventPosition.LastTag,
                    Progress = _lastProcessedEventProgress,
                    StateReason = "",
                    Status = "",
                    LastCheckpoint =
                        String.Format(CultureInfo.InvariantCulture, "{0}", _lastCompletedCheckpointPosition),
                    EventsProcessedAfterRestart = _eventsProcessedAfterRestart,
                    BufferedEvents = -1,
                    WritePendingEventsBeforeCheckpoint =
                        _closingCheckpoint != null ? _closingCheckpoint.GetWritePendingEvents() : 0,
                    WritePendingEventsAfterCheckpoint =
                        _currentCheckpoint != null ? _currentCheckpoint.GetWritePendingEvents() : 0,
                    ReadsInProgress = /*_readDispatcher.ActiveRequestCount*/ +
                                                                             + (_closingCheckpoint != null
                                                                                    ? _closingCheckpoint.
                                                                                          GetReadsInProgress()
                                                                                    : 0)
                                                                             +
                                                                             (_currentCheckpoint != null
                                                                                  ? _currentCheckpoint.
                                                                                        GetReadsInProgress()
                                                                                  : 0),
                    WritesInProgress =
                        ((_inCheckpointWriteAttempt != 0) ? 1 : 0)
                        + (_closingCheckpoint != null ? _closingCheckpoint.GetWritesInProgress() : 0)
                        + (_currentCheckpoint != null ? _currentCheckpoint.GetWritesInProgress() : 0),
                    PartitionsCached = -1,
                    CheckpointStatus =
                        _inCheckpointWriteAttempt > 0
                            ? "Writing (" + _inCheckpointWriteAttempt + ")"
                            : (_inCheckpoint ? "Requested" : ""),
                };
        }

        public void RequestCheckpointToStop()
        {
            EnsureStarted();
            if (!_stopping)
                throw new InvalidOperationException("Not stopping");
            // do not request checkpoint if no events were processed since last checkpoint
            if (_projectionConfig.CheckpointsEnabled
                && _lastCompletedCheckpointPosition < _lastProcessedEventPosition.LastTag)
            {
                RequestCheckpoint(_lastProcessedEventPosition);
                return;
            }
            _coreProjection.Handle(
                new ProjectionMessage.Projections.CheckpointCompleted(_lastCompletedCheckpointPosition));
        }

        public void EventProcessed(
            string state, List<EmittedEvent[]> scheduledWrites, CheckpointTag checkpointTag, float progress)
        {
            EnsureStarted();
            if (_stopping)
                throw new InvalidOperationException("Stopping");
            _eventsProcessedAfterRestart++;
            _lastProcessedEventPosition.UpdateByCheckpointTagForward(checkpointTag);
            _lastProcessedEventProgress = progress;
            // running state only
            if (scheduledWrites != null)
                foreach (var scheduledWrite in scheduledWrites)
                    _currentCheckpoint.EmitEvents(scheduledWrite, _lastProcessedEventPosition.LastTag);
            _handledEventsAfterCheckpoint++;
            _currentProjectionState = state;
            ProcessCheckpoints();
        }

        public void CheckpointSuggested(CheckpointTag checkpointTag, float progress)
        {
            if (!_projectionConfig.CheckpointsEnabled)
                throw new InvalidOperationException("Checkpoints are not enabled");
            EnsureStarted();
            if (_stopping)
                throw new InvalidOperationException("Stopping");
            _lastProcessedEventPosition.UpdateByCheckpointTagForward(checkpointTag);
            _lastProcessedEventProgress = progress;
            RequestCheckpoint(_lastProcessedEventPosition);
        }

        protected void EnsureStarted()
        {
            if (!_started)
                throw new InvalidOperationException("Not started");
        }

        private void RequestCheckpoint(PositionTracker lastProcessedEventPosition)
        {
            if (!_projectionConfig.CheckpointsEnabled)
                throw new InvalidOperationException("Checkpoints are not allowed");
            if (!_inCheckpoint)
                CompleteCheckpoint(lastProcessedEventPosition, _currentProjectionState);
            else
                _coreProjection.Handle(new ProjectionMessage.Projections.PauseRequested());
        }

        private void CompleteCheckpoint(PositionTracker lastProcessedEventPosition, string projectionState)
        {
            CheckpointTag requestedCheckpointPosition = lastProcessedEventPosition.LastTag;
            if (requestedCheckpointPosition == _lastCompletedCheckpointPosition)
                return; // either suggested or requested to stop
            _inCheckpoint = true;
            _requestedCheckpointPosition = requestedCheckpointPosition;
            _requestedCheckpointState = projectionState;
            _handledEventsAfterCheckpoint = 0;

            _closingCheckpoint = _currentCheckpoint;
            _currentCheckpoint = new ProjectionCheckpoint(
                _publisher, this, requestedCheckpointPosition, _projectionConfig.MaxWriteBatchLength, _logger);
            // checkpoint only after assigning new current checkpoint, as it may call back immediately
            _closingCheckpoint.Prepare(requestedCheckpointPosition);
        }

        protected void ProcessCheckpoints()
        {
            if (_projectionConfig.CheckpointsEnabled)
                if (_handledEventsAfterCheckpoint >= _projectionConfig.CheckpointHandledThreshold)
                    RequestCheckpoint(_lastProcessedEventPosition);
                else
                {
                    // TODO: projections emitting events without checkpoints will eat memory by creating new emitted streams  
                }
        }

        protected void CheckpointLoaded(CheckpointTag checkpointTag, string checkpointData)
        {
            _stateLoaded = true;
            _coreProjection.Handle(
                new ProjectionMessage.Projections.CheckpointLoaded(
                    _projectionCorrelationId, checkpointTag, checkpointData));
        }

        protected abstract void BeforeBeginLoadState();
        protected abstract void RequestLoadState();

        public void BeginLoadState()
        {
            if (_stateRequested)
                throw new InvalidOperationException("State has been already requested");
            BeforeBeginLoadState();
            _stateRequested = true;
            if (_projectionConfig.CheckpointsEnabled)
            {
                RequestLoadState();
            }
            else
            {
                CheckpointLoaded(null, null);
            }
        }

        protected abstract void BeginWriteCheckpoint(
            CheckpointTag requestedCheckpointPosition, string requestedCheckpointState);

        public void Handle(ProjectionMessage.Projections.ReadyForCheckpoint message)
        {
            EnsureStarted();
            if (!_inCheckpoint)
                throw new InvalidOperationException();
            BeginWriteCheckpoint(_requestedCheckpointPosition, _requestedCheckpointState);
        }

        protected void CheckpointWritten()
        {
            _lastCompletedCheckpointPosition = _requestedCheckpointPosition;
            _closingCheckpoint = null;
            if (!_stopping)
                // ignore any writes pending in the current checkpoint (this is not the best, but they will never hit the storage, so it is safe)
                _currentCheckpoint.Start();
            _inCheckpoint = false;

            ProcessCheckpoints();
            _coreProjection.Handle(
                new ProjectionMessage.Projections.CheckpointCompleted(_lastCompletedCheckpointPosition));
        }
    }
}
