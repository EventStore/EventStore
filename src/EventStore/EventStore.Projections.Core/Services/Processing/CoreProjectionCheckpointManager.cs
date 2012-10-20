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

namespace EventStore.Projections.Core.Services.Processing
{
    public class CoreProjectionCheckpointManager : IHandle<ProjectionMessage.Projections.ReadyForCheckpoint>
    {
        private readonly IPublisher _publisher;

        private readonly
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>
            _readDispatcher;

        private readonly RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>
            _writeDispatcher;

        private readonly ILogger _logger;
        private readonly ICoreProjection _coreProjection;
        private readonly Guid _projectionCorrelationId;
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
        private int _nextStateIndexToRequest;

        public CoreProjectionCheckpointManager(
            ICoreProjection coreProjection, IPublisher publisher, Guid projectionCorrelationId,
            RequestResponseDispatcher<ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted> readDispatcher,
            RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted> writeDispatcher,
            ProjectionConfig projectionConfig, ILogger logger, string projectionCheckpointStreamId, string name,
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
            _logger = logger;
            _projectionCheckpointStreamId = projectionCheckpointStreamId;
            _name = name;
        }

        public void Start(CheckpointTag checkpointTag)
        {
            if (_started)
                throw new InvalidOperationException("Already started");
            _started = true;
            _lastProcessedEventPosition.UpdateByCheckpointTag(checkpointTag);
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
            if (!_projectionConfig.CheckpointsEnabled)
                throw new InvalidOperationException("Checkpoints are not enabled");
            EnsureStarted();
            if (!_stopping)
                throw new InvalidOperationException("Not stopping");
            // do not request checkpoint if no events were processed since last checkpoint
            if (_lastCompletedCheckpointPosition < _lastProcessedEventPosition.LastTag)
            {
                RequestCheckpoint(_lastProcessedEventPosition);
                return;
            }
            _coreProjection.Handle(
                new ProjectionMessage.Projections.CheckpointCompleted(_lastCompletedCheckpointPosition));
        }

        public void EventProcessed(string state, List<EmittedEvent[]> scheduledWrites, CheckpointTag checkpointTag)
        {
            EnsureStarted();
            if (_stopping)
                throw new InvalidOperationException("Stopping");
            _eventsProcessedAfterRestart++;
            _lastProcessedEventPosition.UpdateByCheckpointTagForward(checkpointTag);
            // running state only
            if (scheduledWrites != null)
                foreach (var scheduledWrite in scheduledWrites)
                    _currentCheckpoint.EmitEvents(scheduledWrite, _lastProcessedEventPosition.LastTag);
            _handledEventsAfterCheckpoint++;
            _currentProjectionState = state;
            ProcessCheckpoints();
        }

        public void CheckpointSuggested(CheckpointTag checkpointTag)
        {
            if (!_projectionConfig.CheckpointsEnabled)
                throw new InvalidOperationException("Checkpoints are not enabled");
            EnsureStarted();
            if (_stopping)
                throw new InvalidOperationException("Stopping");
            _lastProcessedEventPosition.UpdateByCheckpointTagForward(checkpointTag);
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
                _publisher, this, requestedCheckpointPosition, _projectionConfig.MaxWriteBatchLength, _logger);
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
                _coreProjection.Handle(
                    new ProjectionMessage.Projections.CheckpointCompleted(_lastCompletedCheckpointPosition));
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

        public void BeginLoadState()
        {
            _nextStateIndexToRequest = -1; // from the end
            if (_projectionConfig.CheckpointsEnabled)
            {
                RequestLoadState();
            }
            else
            {
                CheckpointLoaded(ExpectedVersion.NoStream, null, null);
            }
        }

        private void RequestLoadState()
        {
            const int recordsToRequest = 10;
            _readDispatcher.Publish(
                new ClientMessage.ReadStreamEventsBackward(
                    Guid.NewGuid(), new SendToThisEnvelope(_coreProjection), _projectionCheckpointStreamId,
                    _nextStateIndexToRequest, recordsToRequest, resolveLinks: false), OnLoadStateReadRequestCompleted);
        }

        private void OnLoadStateReadRequestCompleted(ClientMessage.ReadStreamEventsBackwardCompleted message)
        {
            string checkpointData = null;
            CheckpointTag checkpointTag = null;
            int checkpointEventNumber = -1;
            if (message.Events.Length > 0)
            {
                EventRecord checkpoint = message.Events.FirstOrDefault(v => v.EventType == "ProjectionCheckpoint");
                if (checkpoint != null)
                {
                    checkpointData = Encoding.UTF8.GetString(checkpoint.Data);
                    checkpointTag = checkpoint.Metadata.ParseJson<CheckpointTag>();
                    checkpointEventNumber = checkpoint.EventNumber;
                }
            }

            if (checkpointTag == null && message.NextEventNumber != -1)
            {
                _nextStateIndexToRequest = message.NextEventNumber;
                RequestLoadState();
                return;
            }
            CheckpointLoaded(checkpointEventNumber, checkpointTag, checkpointData);
        }

        private void CheckpointLoaded(int checkpointEventNumber, CheckpointTag checkpointTag, string checkpointData)
        {
            _lastWrittenCheckpointEventNumber = checkpointEventNumber;
            _coreProjection.Handle(
                new ProjectionMessage.Projections.CheckpointLoaded(_projectionCorrelationId, checkpointTag, checkpointData));
        }
    }
}
