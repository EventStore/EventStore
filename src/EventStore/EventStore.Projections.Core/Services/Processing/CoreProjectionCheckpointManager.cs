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
    public class CoreProjectionCheckpointManager
    {
        private readonly CoreProjection _coreProjection;
        private ProjectionCheckpoint _currentCheckpoint;
        private ProjectionCheckpoint _closingCheckpoint;
        private int _handledEventsAfterCheckpoint;
        private CheckpointTag _requestedCheckpointPosition;
        private bool _inCheckpoint;
        private int _inCheckpointWriteAttempt;
        private int _lastWrittenCheckpointEventNumber;
        private string _requestedCheckpointStateJson;
        private CheckpointTag _lastCompletedCheckpointPosition;
        private Event _checkpointEventToBePublished;
        private readonly IPublisher _publisher;
        private readonly ProjectionConfig _projectionConfig;
        private readonly ILogger _logger;
        private readonly string _projectionCheckpointStreamId;
        private bool _recoveryMode;
        internal readonly PositionTracker _lastProcessedEventPosition;

        private int _eventsProcessedAfterRestart;
        private readonly string _name;

        public CoreProjectionCheckpointManager(CoreProjection coreProjection, IPublisher publisher, ProjectionConfig projectionConfig, ILogger logger, string projectionCheckpointStreamId, string name)
        {
            _lastProcessedEventPosition = new PositionTracker(coreProjection._checkpointStrategy.PositionTagger);
            _coreProjection = coreProjection;
            _publisher = publisher;
            _projectionConfig = projectionConfig;
            _logger = logger;
            _projectionCheckpointStreamId = projectionCheckpointStreamId;
            _name = name;
        }

        private void CompleteCheckpoint(PositionTracker lastProcessedEventPosition)
        {
            CheckpointTag requestedCheckpointPosition = lastProcessedEventPosition.LastTag;
            if (requestedCheckpointPosition == _lastCompletedCheckpointPosition)
                return; // either suggested or requested to stop
            _inCheckpoint = true;
            _requestedCheckpointPosition = requestedCheckpointPosition;
            _requestedCheckpointStateJson = _coreProjection.GetProjectionState();
            _handledEventsAfterCheckpoint = 0;

            _closingCheckpoint = _currentCheckpoint;
            _recoveryMode = false;
            _currentCheckpoint = new ProjectionCheckpoint(_publisher, _coreProjection, false, requestedCheckpointPosition, _projectionConfig.MaxWriteBatchLength, _logger);
            // checkpoint only after assigning new current checkpoint, as it may call back immediately
            _closingCheckpoint.Prepare(requestedCheckpointPosition);
        }

        public void WriteCheckpointEventCompleted(ClientMessage.WriteEventsCompleted message)
        {
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
                                                                                    (_lastWrittenCheckpointEventNumber
                                                                                     == ExpectedVersion.NoStream
                                                                                     // account for StreamCreated
                                                                                         ? 1
                                                                                         : 0);

                _closingCheckpoint = null;
                if (_coreProjection._state != CoreProjection.State.Stopping && _coreProjection._state != CoreProjection.State.FaultedStopping)
                    // ignore any writes pending in the current checkpoint (this is not the best, but they will never hit the storage, so it is safe)
                    _currentCheckpoint.Start();
                _inCheckpoint = false;
                _inCheckpointWriteAttempt = 0;

                if (_coreProjection._state != CoreProjection.State.Running)
                {
                    ProcessCheckpoints(_lastProcessedEventPosition);
                    if (_coreProjection._state == CoreProjection.State.Paused)
                    {
                        _coreProjection.TryResume();
                    }
                    else if (_coreProjection._state == CoreProjection.State.Stopping)
                        _coreProjection.GoToState(CoreProjection.State.Stopped);
                    else if (_coreProjection._state == CoreProjection.State.FaultedStopping)
                        _coreProjection.GoToState(CoreProjection.State.Faulted);
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

        public void StartCheckpointManager()
        {
            _requestedCheckpointPosition = null;
            _currentCheckpoint = new ProjectionCheckpoint(_publisher, _coreProjection, _recoveryMode, _lastProcessedEventPosition.LastTag, _projectionConfig.MaxWriteBatchLength, _logger);
            _currentCheckpoint.Start();
        }

        public ProjectionStatistics GetStatistics()
        {
            return new ProjectionStatistics
            {
                Mode = _projectionConfig.Mode,
                Name = null,
                Position = _lastProcessedEventPosition.LastTag,
                StateReason = "",
                Status = (_recoveryMode ? "/Recovery" : ""),
                LastCheckpoint =
                    String.Format(CultureInfo.InvariantCulture, "{0}", _lastCompletedCheckpointPosition),
                EventsProcessedAfterRestart = _eventsProcessedAfterRestart,
                BufferedEvents = -1,
                WritePendingEventsBeforeCheckpoint = _closingCheckpoint != null ? _closingCheckpoint.GetWritePendingEvents() : 0,
                WritePendingEventsAfterCheckpoint = _currentCheckpoint != null ? _currentCheckpoint.GetWritePendingEvents() : 0,
                ReadsInProgress =
                    /*_readDispatcher.ActiveRequestCount*/+
                    + (_closingCheckpoint != null ? _closingCheckpoint.GetReadsInProgress() : 0)
                    + (_currentCheckpoint != null ? _currentCheckpoint.GetReadsInProgress() : 0),
                WritesInProgress =
                    ((_inCheckpointWriteAttempt != 0) ? 1 : 0)
                    + (_closingCheckpoint != null ? _closingCheckpoint.GetWritesInProgress() : 0)
                    + (_currentCheckpoint != null ? _currentCheckpoint.GetWritesInProgress() : 0),
                PartitionsCached = -1,
                CheckpointStatus = _inCheckpointWriteAttempt > 0
                        ? "Writing (" + _inCheckpointWriteAttempt + ")"
                        : (_inCheckpoint ? "Requested" : ""),
            };
        }

        public void SubmitScheduledWritesAndProcessCheckpoints(List<EmittedEvent[]> scheduledWrites)
        {
            if (scheduledWrites != null)
                foreach (var scheduledWrite in scheduledWrites)
                {
                    _currentCheckpoint.EmitEvents(
                        scheduledWrite, _lastProcessedEventPosition.LastTag);
                }
            _handledEventsAfterCheckpoint++;
            if (_coreProjection._state != CoreProjection.State.Faulted && _coreProjection._state != CoreProjection.State.FaultedStopping)
                ProcessCheckpoints(_lastProcessedEventPosition);
        }

        private void ProcessCheckpoints(PositionTracker lastProcessedEventPosition)
        {
            _coreProjection.EnsureState(CoreProjection.State.Running | CoreProjection.State.Paused | CoreProjection.State.Stopping | CoreProjection.State.FaultedStopping);
            if (_projectionConfig.CheckpointsEnabled)
                if (_handledEventsAfterCheckpoint >= _projectionConfig.CheckpointHandledThreshold)
                    RequestCheckpoint(lastProcessedEventPosition);
                else
                {
                    // TODO: projections emitting events without checkpoints will eat memory by creating new emitted streams  
                }
        }

        public void HandleReadyForcheckpoint()
        {
            if (!_inCheckpoint)
                throw new InvalidOperationException();
            // all emitted events caused by events before the checkpoint position have been written  
            // unlock states, so the cache can be clean up as they can now be safely reloaded from the ES
            _coreProjection._partitionStateCache.Unlock(_requestedCheckpointPosition);
            _inCheckpointWriteAttempt = 1;
            //TODO: pass correct expected version
            _checkpointEventToBePublished = new Event(
                Guid.NewGuid(), "ProjectionCheckpoint", false, _requestedCheckpointStateJson == null
                                                                   ? null
                                                                   : Encoding.UTF8.GetBytes(_requestedCheckpointStateJson), _requestedCheckpointPosition.ToJsonBytes());
            PublishWriteCheckpointEvent();
        }

        private void PublishWriteCheckpointEvent()
        {
            if (_logger != null)
                _logger.Trace(
                    "Writing checkpoint for {0} at {1} with expected version number {2}", _name, _requestedCheckpointPosition, _lastWrittenCheckpointEventNumber);
            _publisher.Publish(
                new ClientMessage.WriteEvents(
                    Guid.NewGuid(), new SendToThisEnvelope(_coreProjection), _projectionCheckpointStreamId, _lastWrittenCheckpointEventNumber, _checkpointEventToBePublished));
        }

        public void RequestCheckpoint(PositionTracker lastProcessedEventPosition)
        {
            if (!_projectionConfig.CheckpointsEnabled)
                throw new InvalidOperationException("Checkpoints are not allowed");
            if (!_inCheckpoint)
                CompleteCheckpoint(lastProcessedEventPosition);
            else if (_coreProjection._state != CoreProjection.State.Stopping && _coreProjection._state != CoreProjection.State.FaultedStopping)
                // stopping projection is already paused
                _coreProjection.GoToState(CoreProjection.State.Paused);
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

        public void Initialize()
        {
            _recoveryMode = true;
        }

        public void UpdateLastProcessedEventPosition(ProjectionMessage.Projections.CommittedEventReceived message)
        {
            bool passedFilter = _coreProjection.EventPassesFilter(message);
            if (passedFilter)
            {
                _lastProcessedEventPosition.Update(message);
                _eventsProcessedAfterRestart++;
            }
            else
            {
                if (_coreProjection.EventPassesSourceFilter(message))
                    _lastProcessedEventPosition.Update(message);
                else
                    _lastProcessedEventPosition.UpdatePosition(message.Position);
            }
        }

        public void InitializeNewProjection()
        {
            _lastProcessedEventPosition.UpdateToZero();
            _lastCompletedCheckpointPosition = _lastProcessedEventPosition.LastTag;
            _lastWrittenCheckpointEventNumber = ExpectedVersion.NoStream;
        }

        public void InitializeProjectionFromCheckpoint(EventRecord checkpoint, CheckpointTag checkpointTag)
        {
            _lastProcessedEventPosition.UpdateByCheckpointTag(checkpointTag);
            _lastCompletedCheckpointPosition = checkpointTag;
            _lastWrittenCheckpointEventNumber = checkpoint.EventNumber;
        }

        public void ProcessCheckpointSuggestedEvent(
            ProjectionMessage.Projections.CheckpointSuggested checkpointSuggested)
        {
            _lastProcessedEventPosition.UpdateByCheckpointTagForward(checkpointSuggested.CheckpointTag);
            RequestCheckpoint(_lastProcessedEventPosition);
        }
    }
}