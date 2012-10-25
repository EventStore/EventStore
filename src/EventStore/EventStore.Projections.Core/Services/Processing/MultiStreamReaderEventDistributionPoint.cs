using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class MultiStreamReaderEventDistributionPoint : EventDistributionPoint
    {
        private readonly ILogger _logger = LogManager.GetLoggerFor<MultiStreamReaderEventDistributionPoint>();
        private readonly HashSet<string> _streams;
        private CheckpointTag _fromPositions;
        private readonly bool _resolveLinkTos;

        private bool _paused = true;
        private bool _pauseRequested = true;
        private readonly HashSet<string> _eventsRequested = new HashSet<string>();
        private readonly Dictionary<string, long?> _preparePositions = new Dictionary<string, long?>();
        private int _maxReadCount = 111;
        private bool _disposed;
        private long? _safePositionToJoin;

        public MultiStreamReaderEventDistributionPoint(
            IPublisher publisher, Guid distibutionPointCorrelationId, string[] streams, Dictionary<string, int> fromPositions,
            bool resolveLinkTos)
            : base(publisher, distibutionPointCorrelationId)
        {
            if (streams == null) throw new ArgumentNullException("streams");
            if (streams.Length == 0) throw new ArgumentException("streams");
            _streams = new HashSet<string>(streams);
            var positions = CheckpointTag.FromStreamPositions(fromPositions);
            ValidateTag(positions);
            _fromPositions = positions;
            _resolveLinkTos = resolveLinkTos;
            foreach (var stream in streams)
            {
                _preparePositions.Add(stream, null);
            }
        }

        private void ValidateTag(CheckpointTag fromPositions)
        {
            if (_streams.Count != fromPositions.Streams.Count)
                throw new ArgumentException("Number of streams does not match", "fromPositions");

            foreach (var stream in _streams)
            {
                if (!fromPositions.Streams.ContainsKey(stream))
                    throw new ArgumentException(
                        string.Format("The '{0}' stream position has not been set", stream), "fromPositions");
            }
        }

        public override void Resume()
        {
            if (_disposed) throw new InvalidOperationException("Disposed");
            if (!_pauseRequested)
                throw new InvalidOperationException("Is not paused");
            if (!_paused)
            {
                _pauseRequested = false;
                return;
            }
            _paused = false;
            _pauseRequested = false;
            _logger.Trace(
                "Resuming event distribution {0} at '{1}'", _distibutionPointCorrelationId, _fromPositions);
            RequestEvents(delay: false);
        }

        public override void Pause()
        {
            if (_disposed) throw new InvalidOperationException("Disposed");
            if (_pauseRequested)
                throw new InvalidOperationException("Pause has been already requested");
            _pauseRequested = true;
            if (_eventsRequested.Count == 0)
                _paused = true;
            _logger.Trace(
                "Pausing event distribution {0} at '{1}'", _distibutionPointCorrelationId, _fromPositions);
        }

        public override void Handle(ClientMessage.ReadStreamEventsForwardCompleted message)
        {
            if (_disposed)
                return;
            if (!_streams.Contains(message.EventStreamId))
                throw new InvalidOperationException(
                    string.Format("Invalid stream name: {0}", message.EventStreamId));
            if (!_eventsRequested.Contains(message.EventStreamId))
                throw new InvalidOperationException("Read events has not been requested");
            if (_paused)
                throw new InvalidOperationException("Paused");
            _eventsRequested.Remove(message.EventStreamId);
            switch (message.Result)
            {
                case RangeReadResult.NoStream:
                    UpdateAndDeliverSafePositionToJoin(message.EventStreamId, message.LastCommitPosition.Value); // allow joining heading distribution
                    if (_pauseRequested)
                        _paused = true;
                    else 
                        RequestEvents(delay: true);

                    break;
                case RangeReadResult.Success:
                    if (message.Events.Length == 0)
                    {
                        // the end
                        UpdateAndDeliverSafePositionToJoin(message.EventStreamId, message.LastCommitPosition.Value); // allow joining heading distribution
                    }
                    else
                    {
                        for (int index = 0; index < message.Events.Length; index++)
                        {
                            var @event = message.Events[index].Event;
                            var @link = message.Events[index].Link;
                            EventRecord positionEvent = (link ?? @event);
                            UpdateSafePositionToJoin(positionEvent.EventStreamId, positionEvent.LogPosition);
                            DeliverEvent(@event, @link, 100.0f * (link ?? @event).EventNumber / message.LastEventNumber);
                        }
                    }
                    if (_pauseRequested)
                        _paused = true;
                    else if (message.Events.Length == 0)
                        RequestEvents(delay: true);
                    else
                        _publisher.Publish(CreateTickMessage());
                    break;
                default:
                    throw new NotSupportedException(
                        string.Format("ReadEvents result code was not recognized. Code: {0}", message.Result));
            }
        }

        public override void Handle(ClientMessage.ReadAllEventsForwardCompleted message)
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
            _disposed = true;
        }

        private void RequestEvents(bool delay)
        {

            if (_disposed) throw new InvalidOperationException("Disposed");
            if (_pauseRequested || _paused)
                throw new InvalidOperationException("Paused or pause requested");

            var requested = false;
            foreach (var stream in _streams)
            {
                if (_eventsRequested.Contains(stream))
                    continue;
                requested = true;
                _eventsRequested.Add(stream);

                var readEventsForward = new ClientMessage.ReadStreamEventsForward(
                    _distibutionPointCorrelationId, new SendToThisEnvelope(this), stream, _fromPositions.Streams[stream],
                    _maxReadCount, _resolveLinkTos, returnLastEventNumber: true);
                if (delay)
                    _publisher.Publish(
                        TimerMessage.Schedule.Create(
                            TimeSpan.FromMilliseconds(250), new PublishEnvelope(_publisher, crossThread: true),
                            readEventsForward));
                else
                    _publisher.Publish(readEventsForward);
            }

            // we ignore request events calls that do not lead to any request.  Pending request events call
            // may become obsolete when handling completed read from another stream

        }

        private ProjectionMessage.CoreService.Tick CreateTickMessage()
        {
            return
                new ProjectionMessage.CoreService.Tick(
                    () => { if (!_paused && !_disposed) RequestEvents(delay: false); });
        }

        private void UpdateAndDeliverSafePositionToJoin(string eventStreamId, long preparePosition)
        {
            UpdateSafePositionToJoin(eventStreamId, preparePosition);

            if (_safePositionToJoin == null)
                return; 
            // deliver if already available
            _publisher.Publish(
                new ProjectionMessage.Projections.CommittedEventDistributed(
                    _distibutionPointCorrelationId, default(EventPosition), "", -1,
                    "", -1, false, null, _safePositionToJoin, 100.0f));

        }

        private void UpdateSafePositionToJoin(string streamId, long preparePosition)
        {
            _preparePositions[streamId] = preparePosition;
            if (_preparePositions.All(v => v.Value != null))
                _safePositionToJoin = _preparePositions.Min(v => v.Value);
        }

        private void DeliverEvent(EventRecord @event, EventRecord link, float progress)
        {

            EventRecord positionEvent = (link ?? @event);
            string streamId = positionEvent.EventStreamId;
            int fromPosition = _fromPositions.Streams[streamId];
            if (positionEvent.EventNumber != fromPosition)
                throw new InvalidOperationException(
                    string.Format(
                        "Event number {0} was expected in the stream {1}, but event number {2} was received",
                        fromPosition, streamId, positionEvent.EventNumber));
            _fromPositions = _fromPositions.UpdateStreamPosition(streamId, positionEvent.EventNumber + 1);
            var resolvedLinkTo = streamId != @event.EventStreamId
                                 || positionEvent.EventNumber != @event.EventNumber;
            _publisher.Publish(
                //TODO: publish both link and event data
                new ProjectionMessage.Projections.CommittedEventDistributed(
                    _distibutionPointCorrelationId, default(EventPosition), streamId,
                    positionEvent.EventNumber, @event.EventStreamId, @event.EventNumber, resolvedLinkTo,
                    new Event(@event.EventId, @event.EventType, false, @event.Data, @event.Metadata),
                    _safePositionToJoin, progress));

        }
    }
}