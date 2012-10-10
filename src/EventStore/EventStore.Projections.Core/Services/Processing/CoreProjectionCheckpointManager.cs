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
using System.Text;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class CoreProjectionCheckpointManager : IHandle<ProjectionMessage.Projections.ReadyForCheckpoint>
    {
        private readonly IPublisher _publisher;
        private readonly RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted> _writeDispatcher;
        private readonly ILogger _logger;
        private readonly ICoreProjection _coreProjection;
        private readonly ProjectionConfig _projectionConfig;
        private readonly string _name;
        private readonly string _projectionCheckpointStreamId;

        private ProjectionCheckpoint _currentCheckpoint;
        private ProjectionCheckpoint _closingCheckpoint;
        private int _handledEventsAfterCheckpoint;
        private CheckpointTag _requestedCheckpointPosition;
        private bool _inCheckpoint;
        private int _inCheckpointWriteAttempt;
        private int _lastWrittenCheckpointEventNumber;
        private string _requestedCheckpointState;
        private CheckpointTag _lastCompletedCheckpointPosition;
        private Event _checkpointEventToBePublished;
        private readonly PositionTracker _lastProcessedEventPosition;

        private int _eventsProcessedAfterRestart;
        private bool _started;
        private bool _stopping;
        private string _currentProjectionState;

        public CoreProjectionCheckpointManager(
            ICoreProjection coreProjection, IPublisher publisher,
            RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted> writeDispatcher,
            ProjectionConfig projectionConfig, ILogger logger, string projectionCheckpointStreamId, string name, PositionTagger positionTagger)
        {
            _lastProcessedEventPosition = new PositionTracker(positionTagger);
            _coreProjection = coreProjection;
            _publisher = publisher;
            _writeDispatcher = writeDispatcher;
            _projectionConfig = projectionConfig;
            _logger = logger;
            _projectionCheckpointStreamId = projectionCheckpointStreamId;
            _name = name;
        }

        public void Start(CheckpointTag checkpointTag, int lastWrittenCheckpointEventNumber)
        {
            _started = true;
            _lastProcessedEventPosition.UpdateByCheckpointTag(checkpointTag);
            _lastCompletedCheckpointPosition = checkpointTag;
            _lastWrittenCheckpointEventNumber = lastWrittenCheckpointEventNumber;
            _requestedCheckpointPosition = null;
            _currentCheckpoint = new ProjectionCheckpoint(
                _publisher, this, _lastProcessedEventPosition.LastTag,
                _projectionConfig.MaxWriteBatchLength, _logger);
            _currentCheckpoint.Start();
        }

        public void Stopping()
        {
            _stopping = true;
        }

        public void Stopped()
        {
            _started = false;
        }

        public ProjectionStatistics GetStatistics()
        {
            return new ProjectionStatistics
                {
                    Mode = _projectionConfig.Mode,
                    Name = null,
                    Position = _lastProcessedEventPosition.LastTag,
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
                                                                                  ? _currentCheckpoint.GetReadsInProgress
                                                                                        ()
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

        public void EventProcessed(List<EmittedEvent[]> scheduledWrites, string state)
        {
            // running state only
            if (scheduledWrites != null)
                foreach (var scheduledWrite in scheduledWrites)
                    _currentCheckpoint.EmitEvents(scheduledWrite, _lastProcessedEventPosition.LastTag);
            _handledEventsAfterCheckpoint++;
            _currentProjectionState = state;
            ProcessCheckpoints();
        }

        public bool RequestCheckpointToStop()
        {
            if (_projectionConfig.CheckpointsEnabled)
                // do not request checkpoint if no events were processed since last checkpoint
                if (_lastCompletedCheckpointPosition < _lastProcessedEventPosition.LastTag)
                {
                    RequestCheckpoint(_lastProcessedEventPosition);
                    return true;
                }
            return false;
        }

        public void UpdateLastProcessedEventPosition(CheckpointTag checkpointTag, bool passedFilter)
        {
            if (passedFilter)
                _eventsProcessedAfterRestart++;
            _lastProcessedEventPosition.UpdateByCheckpointTagForward(checkpointTag);
        }

        public void ProcessCheckpointSuggestedEvent(
            ProjectionMessage.Projections.CheckpointSuggested checkpointSuggested)
        {
            _lastProcessedEventPosition.UpdateByCheckpointTagForward(checkpointSuggested.CheckpointTag);
            RequestCheckpoint(_lastProcessedEventPosition);
        }

        public void Handle(ProjectionMessage.Projections.ReadyForCheckpoint message)
        {
            EnsureStarted();
            if (!_inCheckpoint)
                throw new InvalidOperationException();
            _inCheckpointWriteAttempt = 1;
            //TODO: pass correct expected version
            _checkpointEventToBePublished = new Event(
                Guid.NewGuid(), "ProjectionCheckpoint", false,
                _requestedCheckpointState == null ? null : Encoding.UTF8.GetBytes(_requestedCheckpointState),
                _requestedCheckpointPosition.ToJsonBytes());
            PublishWriteCheckpointEvent();
        }

        private void EnsureStarted()
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
                _publisher, this, requestedCheckpointPosition, _projectionConfig.MaxWriteBatchLength,
                _logger);
            // checkpoint only after assigning new current checkpoint, as it may call back immediately
            _closingCheckpoint.Prepare(requestedCheckpointPosition);
        }

        private void WriteCheckpointEventCompleted(ClientMessage.WriteEventsCompleted message)
        {
            EnsureStarted();
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
                                                    + (_lastWrittenCheckpointEventNumber == ExpectedVersion.NoStream
                                                       // account for StreamCreated
                                                           ? 1
                                                           : 0);

                _closingCheckpoint = null;
                if (!_stopping)
                    // ignore any writes pending in the current checkpoint (this is not the best, but they will never hit the storage, so it is safe)
                    _currentCheckpoint.Start();
                _inCheckpoint = false;
                _inCheckpointWriteAttempt = 0;

                ProcessCheckpoints();
                _coreProjection.Handle(new ProjectionMessage.Projections.CheckpointCompleted(_lastCompletedCheckpointPosition));
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

        private void ProcessCheckpoints()
        {
            if (_projectionConfig.CheckpointsEnabled)
                if (_handledEventsAfterCheckpoint >= _projectionConfig.CheckpointHandledThreshold)
                    RequestCheckpoint(_lastProcessedEventPosition);
                else
                {
                    // TODO: projections emitting events without checkpoints will eat memory by creating new emitted streams  
                }
        }

        private void PublishWriteCheckpointEvent()
        {
            if (_logger != null)
                _logger.Trace(
                    "Writing checkpoint for {0} at {1} with expected version number {2}", _name,
                    _requestedCheckpointPosition, _lastWrittenCheckpointEventNumber);
            _writeDispatcher.Publish(
                new ClientMessage.WriteEvents(
                    Guid.NewGuid(), new SendToThisEnvelope(_writeDispatcher), _projectionCheckpointStreamId,
                    _lastWrittenCheckpointEventNumber, _checkpointEventToBePublished), WriteCheckpointEventCompleted);
        }
    }
}
