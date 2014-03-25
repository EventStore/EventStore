using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core.Messages;
using Newtonsoft.Json.Linq;

namespace EventStore.Projections.Core.Services.Processing
{
    public class EmittedStream : IDisposable, IHandle<CoreProjectionProcessingMessage.EmittedStreamWriteCompleted>
    {
        private readonly IODispatcher _ioDispatcher;


        private readonly ILogger _logger;
        private readonly string _streamId;
        private readonly WriterConfiguration _writerConfiguration;
        private readonly ProjectionVersion _projectionVersion;
        private readonly IPrincipal _writeAs;
        private readonly PositionTagger _positionTagger;
        private readonly CheckpointTag _zeroPosition;
        private readonly CheckpointTag _fromCheckpointPosition;
        private readonly IEmittedStreamContainer _readyHandler;

        private readonly Stack<Tuple<CheckpointTag, string, int>> _alreadyCommittedEvents =
            new Stack<Tuple<CheckpointTag, string, int>>();
        private readonly Queue<EmittedEvent> _pendingWrites = new Queue<EmittedEvent>();

        private bool _checkpointRequested;
        private bool _awaitingWriteCompleted;
        private bool _awaitingMetadataWriteCompleted;
        private bool _awaitingReady;
        private bool _awaitingListEventsCompleted;
        private bool _started;

        private readonly int _maxWriteBatchLength;
        private CheckpointTag _lastCommittedOrSubmittedEventPosition; // TODO: rename
        private bool _metadataStreamCreated;
        private CheckpointTag _lastQueuedEventPosition;
        private Event[] _submittedToWriteEvents;
        private EmittedEvent[] _submittedToWriteEmittedEvents;
        private int _lastKnownEventNumber = ExpectedVersion.Invalid;
        private int _retrievedNextEventNumber = ExpectedVersion.Invalid;
        private readonly bool _noCheckpoints;
        private bool _disposed;
        private bool _recoveryCompleted;
        private Event _submittedWriteMetaStreamEvent;


        public class WriterConfiguration
        {
            private readonly IPrincipal _writeAs;
            private readonly int _maxWriteBatchLength;
            private readonly ILogger _logger;

            private readonly int? maxCount;
            private readonly TimeSpan? maxAge;

            public class StreamMetadata
            {
                private readonly int? _maxCount;
                private readonly TimeSpan? _maxAge;

                public StreamMetadata(int? maxCount = null, TimeSpan? maxAge = null)
                {
                    _maxCount = maxCount;
                    _maxAge = maxAge;
                }

                public int? MaxCount
                {
                    get { return _maxCount; }
                }

                public TimeSpan? MaxAge
                {
                    get { return _maxAge; }
                }
            }

            public WriterConfiguration(
                StreamMetadata streamMetadata, IPrincipal writeAs, int maxWriteBatchLength, ILogger logger = null)
            {
                _writeAs = writeAs;
                _maxWriteBatchLength = maxWriteBatchLength;
                _logger = logger;
                if (streamMetadata != null)
                {
                    this.maxCount = streamMetadata.MaxCount;
                    this.maxAge = streamMetadata.MaxAge;
                }
            }

            public IPrincipal WriteAs
            {
                get { return _writeAs; }
            }

            public int MaxWriteBatchLength
            {
                get { return _maxWriteBatchLength; }
            }

            public ILogger Logger
            {
                get { return _logger; }
            }

            public int? MaxCount
            {
                get { return maxCount; }
            }

            public TimeSpan? MaxAge
            {
                get { return maxAge; }
            }
        }

        public EmittedStream(
            string streamId, WriterConfiguration writerConfiguration, ProjectionVersion projectionVersion,
            PositionTagger positionTagger, CheckpointTag fromCheckpointPosition, IODispatcher ioDispatcher,
            IEmittedStreamContainer readyHandler, bool noCheckpoints = false)
        {
            if (streamId == null) throw new ArgumentNullException("streamId");
            if (writerConfiguration == null) throw new ArgumentNullException("writerConfiguration");
            if (positionTagger == null) throw new ArgumentNullException("positionTagger");
            if (fromCheckpointPosition == null) throw new ArgumentNullException("fromCheckpointPosition");
            if (ioDispatcher == null) throw new ArgumentNullException("ioDispatcher");
            if (readyHandler == null) throw new ArgumentNullException("readyHandler");
            if (streamId == "") throw new ArgumentException("streamId");
            _streamId = streamId;
            _writerConfiguration = writerConfiguration;
            _projectionVersion = projectionVersion;
            _writeAs = writerConfiguration.WriteAs;
            _positionTagger = positionTagger;
            _zeroPosition = positionTagger.MakeZeroCheckpointTag();
            _fromCheckpointPosition = fromCheckpointPosition;
            _lastQueuedEventPosition = null;
            _ioDispatcher = ioDispatcher;
            _readyHandler = readyHandler;
            _maxWriteBatchLength = writerConfiguration.MaxWriteBatchLength;
            _logger = writerConfiguration.Logger;
            _noCheckpoints = noCheckpoints;
        }

        public void EmitEvents(EmittedEvent[] events)
        {
            if (events == null) throw new ArgumentNullException("events");
            CheckpointTag groupCausedBy = null;
            foreach (var @event in events)
            {
                if (groupCausedBy == null)
                {
                    groupCausedBy = @event.CausedByTag;
                    if (!(_lastQueuedEventPosition != null && groupCausedBy > _lastQueuedEventPosition) && !(_lastQueuedEventPosition == null && groupCausedBy >= _fromCheckpointPosition))
                        throw new InvalidOperationException(
                            string.Format("Invalid event order.  '{0}' goes after '{1}'", @event.CausedByTag, _lastQueuedEventPosition));
                    _lastQueuedEventPosition = groupCausedBy;
                }
                else if (@event.CausedByTag != groupCausedBy)
                    throw new ArgumentException("events must share the same CausedByTag");
                if (@event.StreamId != _streamId)
                    throw new ArgumentException("Invalid streamId", "events");
            }
            EnsureCheckpointNotRequested();
            foreach (var @event in events)
                _pendingWrites.Enqueue(@event);
            ProcessWrites();
        }

        public void Checkpoint()
        {
            EnsureCheckpointsEnabled();
            EnsureStreamStarted();
            EnsureCheckpointNotRequested();
            _checkpointRequested = true;
            ProcessRequestedCheckpoint();
        }

        public void Start()
        {
            EnsureCheckpointNotRequested();
            if (_started)
                throw new InvalidOperationException("Stream is already started");
            _started = true;
            ProcessWrites();
        }

        public int GetWritePendingEvents()
        {
            return _pendingWrites.Count;
        }

        public int GetWritesInProgress()
        {
            return (_awaitingWriteCompleted ? 1 : 0) + (_awaitingMetadataWriteCompleted ? 1 : 0);
        }

        public int GetReadsInProgress()
        {
            return _awaitingListEventsCompleted ? 1 : 0;
        }

        public void Handle(ClientMessage.WriteEventsCompleted message)
        {
            if (!_awaitingWriteCompleted)
                throw new InvalidOperationException("WriteEvents has not been submitted");
            if (_disposed)
                return;
            _awaitingWriteCompleted = false;
            if (message.Result == OperationResult.Success)
            {
                _lastKnownEventNumber = message.FirstEventNumber + _submittedToWriteEvents.Length - 1;
                NotifyEventsCommitted(_submittedToWriteEmittedEvents, message.FirstEventNumber);
                OnWriteCompleted();
                return;
            }
            if (_logger != null)
            {
                _logger.Info("Failed to write events to stream {0}. Error: {1}",
                             _streamId,
                             Enum.GetName(typeof (OperationResult), message.Result));
            }
            switch (message.Result)
            {
                case OperationResult.WrongExpectedVersion:
                    RequestRestart(string.Format("The '{0}' stream has be written to from the outside", _streamId));
                    break;
                case OperationResult.PrepareTimeout:
                case OperationResult.ForwardTimeout:
                case OperationResult.CommitTimeout:
                    if (_logger != null) _logger.Info("Retrying write to {0}", _streamId);
                    PublishWriteEvents();
                    break;
                default:
                    throw new NotSupportedException("Unsupported error code received");
            }
        }

        private void RequestRestart(string reason)
        {
            _readyHandler.Handle(new CoreProjectionProcessingMessage.RestartRequested(Guid.Empty, reason));
        }

        private void Failed(string reason)
        {
            _readyHandler.Handle(new CoreProjectionProcessingMessage.Failed(Guid.Empty, reason));
        }

        private void ReadStreamEventsBackwardCompleted(ClientMessage.ReadStreamEventsBackwardCompleted message, CheckpointTag lastCheckpointPosition)
        {
//            if (lastCheckpointPosition == _zeroPosition)
//                throw new ArgumentException("lastCheckpointPosition cannot be equal to zero position");

            if (!_awaitingListEventsCompleted)
                throw new InvalidOperationException("ReadStreamEventsBackward has not been requested");
            if (_disposed)
                return;
            _awaitingListEventsCompleted = false;

            var newPhysicalStream = message.Events.Length == 0;
            _retrievedNextEventNumber = newPhysicalStream
                ? (message.StreamMetadata != null ? (message.StreamMetadata.TruncateBefore ?? 0) : 0)
                : message.LastEventNumber + 1;

            if (_lastCommittedOrSubmittedEventPosition == null)
            {
                var parsed = default(CheckpointTagVersion);
                if (!newPhysicalStream)
                {
                    parsed = message.Events[0].Event.Metadata.ParseCheckpointTagVersionExtraJson(_projectionVersion);
                    if (_projectionVersion.ProjectionId != parsed.Version.ProjectionId)
                    {
                        Failed(
                            string.Format(
                                "Multiple projections emitting to the same stream detected.  Stream: '{0}'. Last event projection: '{1}'.  Emitting projection: '{2}'",
                                _streamId, parsed.Version.ProjectionId, _projectionVersion.ProjectionId));
                        return;
                    }
                }
                var newLogicalStream = newPhysicalStream
                    || (_projectionVersion.ProjectionId != parsed.Version.ProjectionId || _projectionVersion.Epoch > parsed.Version.Version);

                _lastKnownEventNumber = newPhysicalStream ? ExpectedVersion.NoStream : message.Events[0].Event.EventNumber;

                //TODO: throw exception when _projectionVersion.ProjectionId != parsed.ProjectionId ?

                if (newLogicalStream)
                {
                    _lastCommittedOrSubmittedEventPosition = _zeroPosition;
                    _metadataStreamCreated = false;
                }
                else
                {
                    //TODO: verify order - as we are reading backward
                    _lastCommittedOrSubmittedEventPosition = parsed.AdjustBy(_positionTagger, _projectionVersion);
                    _metadataStreamCreated = true; // should exist or no need to create
                }
            }

            var stop = CollectAlreadyCommittedEvents(message, lastCheckpointPosition);

            if (stop)
                try
                {
                    SubmitWriteEventsInRecovery();
                }
                catch (InvalidEmittedEventSequenceExceptioin ex)
                {
                    Failed(ex.Message);
                }
            else
                SubmitListEvents(lastCheckpointPosition, message.NextEventNumber);

        }

        private bool CollectAlreadyCommittedEvents(
            ClientMessage.ReadStreamEventsBackwardCompleted message, CheckpointTag lastCheckpointPosition)
        {
            var stop = false;
            foreach (var e in message.Events)
            {
                var checkpointTagVersion = e.Event.Metadata.ParseCheckpointTagVersionExtraJson(_projectionVersion);
                var ourEpoch = checkpointTagVersion.Version.ProjectionId == _projectionVersion.ProjectionId
                               && checkpointTagVersion.Version.Version >= _projectionVersion.Epoch;

                if (IsV1StreamCreatedEvent(e))
                    continue;

                if (checkpointTagVersion.Tag == null)
                {
                    Failed(
                        string.Format(
                            "A unstamped event found. Stream: '{0}'. EventNumber: '{1}'", message.EventStreamId,
                            e.OriginalEventNumber));
                    return true;
                }
                var doStop = !ourEpoch;
                if (!doStop)
                {
                    //NOTE: may need to compare with last pre-recorded event
                    //      but should not push to alreadyCommitted if source changed (must be at checkpoint)
                    var adjustedTag = checkpointTagVersion.AdjustBy(_positionTagger, _projectionVersion);
                    doStop = adjustedTag <= lastCheckpointPosition;
                }
                if (doStop)
                    // ignore any events prior to the requested lastCheckpointPosition (== first emitted event position)
                {
                    stop = true;
                    break;
                }
                var eventType = e.Event.EventType;
                _alreadyCommittedEvents.Push(Tuple.Create(checkpointTagVersion.Tag, eventType, e.Event.EventNumber));
            }
            return stop || message.IsEndOfStream;
        }

        private static bool IsV1StreamCreatedEvent(EventStore.Core.Data.ResolvedEvent e)
        {
            return e.Link == null && e.OriginalEventNumber == 0
                   && (e.OriginalEvent.EventType == SystemEventTypes.V1__StreamCreatedImplicit__
                       || e.OriginalEvent.EventType == SystemEventTypes.V1__StreamCreated__);
        }

        private void ProcessWrites()
        {
            if (_started && !_awaitingListEventsCompleted && !_awaitingWriteCompleted
                && !_awaitingMetadataWriteCompleted && _pendingWrites.Count > 0)
            {
                var firstEvent = _pendingWrites.Peek();
                if (_lastCommittedOrSubmittedEventPosition == null)
                    SubmitListEvents(_fromCheckpointPosition);
                else
                    SubmitWriteEventsInRecovery();
            }
        }

        private void SubmitListEvents(CheckpointTag upTo, int fromEventNumber = -1)
        {
            if (_awaitingWriteCompleted || _awaitingMetadataWriteCompleted || _awaitingListEventsCompleted)
                throw new Exception();
            _awaitingListEventsCompleted = true;
            _ioDispatcher.ReadBackward(
                //TODO: reading events history in batches of 1 event (slow?)
                _streamId, fromEventNumber, 1, resolveLinks: false, principal: SystemAccount.Principal,
                action: completed => ReadStreamEventsBackwardCompleted(completed, upTo));
        }

        private void SubmitWriteMetadata()
        {
            if (_awaitingWriteCompleted || _awaitingMetadataWriteCompleted || _awaitingListEventsCompleted)
                throw new Exception();
            var streamAcl = _streamId.StartsWith("$")
                ? new StreamAcl(SystemRoles.All, null, null, SystemRoles.All, null)
                : new StreamAcl((string)null, null, null, null, null);

            var streamMetadata = new StreamMetadata(
                _writerConfiguration.MaxCount, _writerConfiguration.MaxAge, acl: streamAcl,
                truncateBefore: _retrievedNextEventNumber == 0 ? (int?) null : _retrievedNextEventNumber);

            _submittedWriteMetaStreamEvent = new Event(
                Guid.NewGuid(), SystemEventTypes.StreamMetadata, true, streamMetadata.ToJsonBytes(), null);

            _awaitingMetadataWriteCompleted = true;

            PublishWriteMetaStream();
        }

        private void PublishWriteMetaStream()
        {
            _ioDispatcher.WriteEvent(
                SystemStreams.MetastreamOf(_streamId), ExpectedVersion.Any, _submittedWriteMetaStreamEvent, _writeAs,
                HandleMetadataWriteCompleted);
        }

        private void HandleMetadataWriteCompleted(ClientMessage.WriteEventsCompleted message)
        {
            if (!_awaitingMetadataWriteCompleted)
                throw new InvalidOperationException("WriteEvents to metadata stream has not been submitted");
            if (_disposed)
                return;
            if (message.Result == OperationResult.Success)
            {
                _metadataStreamCreated = true;
                _awaitingMetadataWriteCompleted = false;
                PublishWriteEvents();
                return;
            }
            if (_logger != null)
            {
                _logger.Info("Failed to write events to stream {0}. Error: {1}",
                             SystemStreams.MetastreamOf(_streamId),
                             Enum.GetName(typeof(OperationResult), message.Result));
            }
            switch (message.Result)
            {
                case OperationResult.WrongExpectedVersion:
                    RequestRestart(string.Format("The '{0}' stream has be written to from the outside", _streamId));
                    break;
                case OperationResult.PrepareTimeout:
                case OperationResult.ForwardTimeout:
                case OperationResult.CommitTimeout:
                    if (_logger != null) _logger.Info("Retrying write to {0}", _streamId);
                    PublishWriteMetaStream();
                    break;
                default:
                    throw new NotSupportedException("Unsupported error code received");
            }
        }

        private void SubmitWriteEvents()
        {
            if (_awaitingWriteCompleted || _awaitingMetadataWriteCompleted || _awaitingListEventsCompleted)
                throw new Exception();
            if (!_metadataStreamCreated)
                if (_lastCommittedOrSubmittedEventPosition != _zeroPosition)
                    throw new Exception("Internal error");
            var events = new List<Event>();
            var emittedEvents = new List<EmittedEvent>();
            while (_pendingWrites.Count > 0 && events.Count < _maxWriteBatchLength)
            {
                var e = _pendingWrites.Peek();
                if (!e.IsReady())
                {
                    _readyHandler.Handle(
                        new CoreProjectionProcessingMessage.EmittedStreamAwaiting(
                            _streamId, new SendToThisEnvelope(this)));
                    _awaitingReady = true;
                    break;
                }
                _pendingWrites.Dequeue();

                var expectedTag = e.ExpectedTag;
                var causedByTag = e.CausedByTag;
                if (expectedTag != null)
                    if (DetectConcurrencyViolations(expectedTag))
                    {
                        RequestRestart(
                            string.Format(
                                "Wrong expected tag while submitting write event request to the '{0}' stream.  The last known stream tag is: '{1}'  the expected tag is: '{2}'",
                                _streamId, _lastCommittedOrSubmittedEventPosition, expectedTag));
                        return;
                    }
                _lastCommittedOrSubmittedEventPosition = causedByTag;
                events.Add(
                    new Event(
                        e.EventId, e.EventType, e.IsJson, e.Data != null ? Helper.UTF8NoBom.GetBytes(e.Data) : null,
                        e.CausedByTag.ToJsonBytes(_projectionVersion, MetadataWithCausedByAndCorrelationId(e))));
                emittedEvents.Add(e);
            }
            _submittedToWriteEvents = events.ToArray();
            _submittedToWriteEmittedEvents = emittedEvents.ToArray();

            if (_submittedToWriteEvents.Length > 0)
                PublishWriteEvents();
        }

        private IEnumerable<KeyValuePair<string, JToken>> MetadataWithCausedByAndCorrelationId(
            EmittedEvent emittedEvent)
        {
            var extraMetaData = emittedEvent.ExtraMetaData();
            var correlationIdFound = false;
            if (extraMetaData != null)
                foreach (var valuePair in from pair in extraMetaData
                                          where pair.Key != "$causedBy"
                                          select pair)
                {
                    if (valuePair.Key == "$correlationId")
                        correlationIdFound = true;
                    yield return new KeyValuePair<string, JToken>(valuePair.Key, new JRaw(valuePair.Value));
                }
            if (emittedEvent.CausedBy != Guid.Empty)
                yield return
                    new KeyValuePair<string, JToken>(
                        "$causedBy", JValue.CreateString(emittedEvent.CausedBy.ToString("D")));
            if (!correlationIdFound && !string.IsNullOrEmpty(emittedEvent.CorrelationId))
                yield return
                    new KeyValuePair<string, JToken>("$correlationId", JValue.CreateString(emittedEvent.CorrelationId));
        }

        private bool DetectConcurrencyViolations(CheckpointTag expectedTag)
        {
            //NOTE: the comment below is not longer actual
            //      Keeping it for reference only
            //      We do back-read all the streams when loading state, so we know exactly which version to expect

            //TODO: if the following statement is about event order stream - let write null event into this stream
            //NOTE: the following condition is only meant to detect concurrency violations when
            // another instance of the projection (running in the another node etc) has been writing to 
            // the same stream.  However, the expected tag sometimes can be greater than last actually written tag
            // This happens when a projection is restarted from a checkpoint and the checkpoint has been made at 
            // position not updating the projection state 
            return expectedTag != _lastCommittedOrSubmittedEventPosition;
        }

        private void PublishWriteEvents()
        {
            if (!_metadataStreamCreated)
            {
                SubmitWriteMetadata();
                return;
            }
            _awaitingWriteCompleted = true;
            _ioDispatcher.WriteEvents(_streamId, _lastKnownEventNumber, _submittedToWriteEvents, _writeAs, Handle);

        }

        private void EnsureCheckpointNotRequested()
        {
            if (_checkpointRequested)
                throw new InvalidOperationException("Checkpoint requested");
        }

        private void EnsureStreamStarted()
        {
            if (!_started)
                throw new InvalidOperationException("Not started");
        }

        private void OnWriteCompleted()
        {
            NotifyWriteCompleted();
            ProcessWrites();
            ProcessRequestedCheckpoint();
        }

        private void NotifyWriteCompleted()
        {
            _readyHandler.Handle(new CoreProjectionProcessingMessage.EmittedStreamWriteCompleted(_streamId));
        }

        private void ProcessRequestedCheckpoint()
        {
            if (_checkpointRequested && !_awaitingWriteCompleted && !_awaitingMetadataWriteCompleted
                && _pendingWrites.Count == 0)
            {
                EnsureCheckpointsEnabled();
                _readyHandler.Handle(new CoreProjectionProcessingMessage.ReadyForCheckpoint(this));
            }
        }

        private void EnsureCheckpointsEnabled()
        {
            if (_noCheckpoints)
                throw new InvalidOperationException("Checkpoints disabled");
        }

        private void SubmitWriteEventsInRecovery()
        {
            bool anyFound = false;
            while (_pendingWrites.Count > 0)
            {
                var eventToWrite = _pendingWrites.Peek();
                if (eventToWrite.CausedByTag > _lastCommittedOrSubmittedEventPosition || _alreadyCommittedEvents.Count == 0)
                    RecoveryCompleted();
                if (_recoveryCompleted)
                {
                    if (anyFound)
                        NotifyWriteCompleted(); // unlock pending write-resolves if any
                    SubmitWriteEvents();
                    return;
                }
                var topAlreadyCommitted = ValidateEmittedEventInRecoveryMode(eventToWrite);
                if (topAlreadyCommitted == null)
                    continue; // means skipped one already comitted item due to deleted stream handling
                anyFound = true;
                NotifyEventCommitted(eventToWrite, topAlreadyCommitted.Item3); 
                _pendingWrites.Dequeue(); // drop already committed event
            }
            OnWriteCompleted();
        }

        private Tuple<CheckpointTag, string, int> ValidateEmittedEventInRecoveryMode(EmittedEvent eventsToWrite)
        {
            var topAlreadyCommitted = _alreadyCommittedEvents.Pop();
            if (topAlreadyCommitted.Item1 < eventsToWrite.CausedByTag)
                return null;
            var failed = topAlreadyCommitted.Item1 != eventsToWrite.CausedByTag || topAlreadyCommitted.Item2 != eventsToWrite.EventType;
            if (failed)
                throw new InvalidEmittedEventSequenceExceptioin(
                    string.Format(
                        "An event emitted in recovery differ from the originally emitted event.  Existing('{0}', '{1}'). New('{2}', '{3}')",
                        topAlreadyCommitted.Item2, topAlreadyCommitted.Item1, eventsToWrite.EventType, eventsToWrite.CausedByTag));
            return topAlreadyCommitted;
        }

        private void RecoveryCompleted()
        {
            _recoveryCompleted = true;
        }

        private static void NotifyEventsCommitted(EmittedEvent[] events, int firstEventNumber)
        {
            var sequenceNumber = firstEventNumber;
            foreach (var e in events)
                NotifyEventCommitted(e, sequenceNumber++);
        }

        private static void NotifyEventCommitted(EmittedEvent @event, int eventNumber)
        {
            if (@event.OnCommitted != null)
                @event.OnCommitted(eventNumber);
        }

        public void Dispose()
        {
            _disposed = true;
        }

        public void Handle(CoreProjectionProcessingMessage.EmittedStreamWriteCompleted message)
        {
            if (!_awaitingReady)
                throw new InvalidOperationException("AwaitingReady state required");
            ProcessWrites();
        }
    }

    class InvalidEmittedEventSequenceExceptioin : Exception
    {
        public InvalidEmittedEventSequenceExceptioin(string message)
            : base(message)
        {
        }
    }
}
