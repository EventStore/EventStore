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

        // event, link, progress
        private readonly Dictionary<string, Queue<Tuple<EventRecord, EventRecord, float>>> _buffers =
            new Dictionary<string, Queue<Tuple<EventRecord, EventRecord, float>>>();

        private int _maxReadCount = 111;
        private bool _disposed;
        private long? _safePositionToJoin;

        public MultiStreamReaderEventDistributionPoint(
            IPublisher publisher, Guid distibutionPointCorrelationId, string[] streams,
            Dictionary<string, int> fromPositions, bool resolveLinkTos)
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
            _logger.Trace("Resuming event distribution {0} at '{1}'", _distibutionPointCorrelationId, _fromPositions);
            RequestEventsAll();
        }

        public override void Pause()
        {
            if (_disposed) throw new InvalidOperationException("Disposed");
            if (_pauseRequested)
                throw new InvalidOperationException("Pause has been already requested");
            _pauseRequested = true;
            if (_eventsRequested.Count == 0)
                _paused = true;
            _logger.Trace("Pausing event distribution {0} at '{1}'", _distibutionPointCorrelationId, _fromPositions);
        }

        public override void Handle(ClientMessage.ReadStreamEventsForwardCompleted message)
        {
            if (_disposed)
                return;
            if (!_streams.Contains(message.EventStreamId))
                throw new InvalidOperationException(string.Format("Invalid stream name: {0}", message.EventStreamId));
            if (!_eventsRequested.Contains(message.EventStreamId))
                throw new InvalidOperationException("Read events has not been requested");
            if (_paused)
                throw new InvalidOperationException("Paused");
            _eventsRequested.Remove(message.EventStreamId);
            switch (message.Result)
            {
                case RangeReadResult.NoStream:
                    UpdateSafePositionToJoin(message.EventStreamId, message.LastCommitPosition.Value);
                    ProcessBuffers();
                    if (_pauseRequested)
                        _paused = true;
                    else
                        RequestEvents(message.EventStreamId, delay: true);
                    _publisher.Publish(CreateTickMessage());
                    break;
                case RangeReadResult.Success:
                    if (message.Events.Length == 0)
                    {
                        // the end
                        UpdateSafePositionToJoin(message.EventStreamId, message.LastCommitPosition.Value);
                    }
                    else
                    {
                        for (int index = 0; index < message.Events.Length; index++)
                        {
                            var @event = message.Events[index].Event;
                            var @link = message.Events[index].Link;
                            EventRecord positionEvent = (link ?? @event);
                            UpdateSafePositionToJoin(positionEvent.EventStreamId, positionEvent.LogPosition);
                            Queue<Tuple<EventRecord, EventRecord, float>> queue;
                            if (!_buffers.TryGetValue(positionEvent.EventStreamId, out queue))
                            {
                                queue = new Queue<Tuple<EventRecord, EventRecord, float>>();
                                _buffers.Add(positionEvent.EventStreamId, queue);
                            }
                            //TODO: progress calculation below is incorrect.  sum(current)/sum(last_event) where sum by all streams
                            queue.Enqueue(Tuple.Create(@event, positionEvent, 100.0f*(link ?? @event).EventNumber/message.LastEventNumber.Value));
                        }
                    }
                    ProcessBuffers();
                    if (_pauseRequested)
                        _paused = true;
                    else if (message.Events.Length == 0)
                        RequestEvents(message.EventStreamId, delay: true);
                    _publisher.Publish(CreateTickMessage());
                    break;
                default:
                    throw new NotSupportedException(
                        string.Format("ReadEvents result code was not recognized. Code: {0}", message.Result));
            }
        }

        private void ProcessBuffers()
        {
            if (_safePositionToJoin == null)
                return;
            while (true)
            {
                var anyNonEmpty = false;
                var minPosition = long.MaxValue;
                var minStreamId = "";
                var any = false;
                foreach (var buffer in _buffers)
                {
                    if (buffer.Value.Count == 0)
                        continue;
                    anyNonEmpty = true;
                    var head = buffer.Value.Peek();
                    var currentStreamId = buffer.Key;
                    var positionEvent = head.Item2;
                    if (positionEvent.LogPosition <= _safePositionToJoin && positionEvent.LogPosition < minPosition)
                    {
                        minPosition = positionEvent.LogPosition;
                        minStreamId = currentStreamId;
                        any = true;
                    }
                }
                if (!any)
                {
                    if (!anyNonEmpty)
                        DeliverSafePositionToJoin();
                    break;
                }
                var minHead = _buffers[minStreamId].Dequeue();
                DeliverEvent(minHead.Item1, minHead.Item2, minHead.Item3);
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

        private void RequestEventsAll()
        {
            if (_pauseRequested || _paused)
                return;
            foreach (var stream in _streams)
                RequestEvents(stream, delay: false);
        }

        private void RequestEvents(string stream, bool delay)
        {
            if (_disposed) throw new InvalidOperationException("Disposed");
            if (_pauseRequested || _paused)
                throw new InvalidOperationException("Paused or pause requested");

            if (_eventsRequested.Contains(stream))
                return;
            Queue<Tuple<EventRecord, EventRecord, float>> queue;
            if (_buffers.TryGetValue(stream, out queue) && queue.Count > 0)
                return;
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

        private ProjectionMessage.CoreService.Tick CreateTickMessage()
        {
            return
                new ProjectionMessage.CoreService.Tick(
                    () => { if (!_disposed) RequestEventsAll(); });
        }

        private void DeliverSafePositionToJoin()
        {
            if (_safePositionToJoin == null)
                return;
            // deliver if already available
            _publisher.Publish(
                new ProjectionMessage.Projections.CommittedEventDistributed(
                    _distibutionPointCorrelationId, default(EventPosition), "", -1, "", -1, false, null,
                    _safePositionToJoin, 100.0f));
        }

        private void UpdateSafePositionToJoin(string streamId, long preparePosition)
        {
            _preparePositions[streamId] = preparePosition;
            if (_preparePositions.All(v => v.Value != null))
                _safePositionToJoin = _preparePositions.Min(v => v.Value);
        }

        private void DeliverEvent(EventRecord @event, EventRecord positionEvent, float progress)
        {
            string streamId = positionEvent.EventStreamId;
            int fromPosition = _fromPositions.Streams[streamId];
            if (positionEvent.EventNumber != fromPosition)
                throw new InvalidOperationException(
                    string.Format(
                        "Event number {0} was expected in the stream {1}, but event number {2} was received",
                        fromPosition, streamId, positionEvent.EventNumber));
            _fromPositions = _fromPositions.UpdateStreamPosition(streamId, positionEvent.EventNumber + 1);
            var resolvedLinkTo = streamId != @event.EventStreamId || positionEvent.EventNumber != @event.EventNumber;
            _publisher.Publish(
                //TODO: publish both link and event data
                new ProjectionMessage.Projections.CommittedEventDistributed(
                    _distibutionPointCorrelationId, default(EventPosition), streamId, positionEvent.EventNumber,
                    @event.EventStreamId, @event.EventNumber, resolvedLinkTo,
                    new Event(@event.EventId, @event.EventType, false, @event.Data, @event.Metadata),
                    positionEvent.LogPosition, progress));
        }
    }
}
