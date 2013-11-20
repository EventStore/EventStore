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
using System.Security.Principal;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class MultiStreamEventReader : EventReader, IHandle<ClientMessage.ReadStreamEventsForwardCompleted>
    {
        private readonly HashSet<string> _streams;
        private CheckpointTag _fromPositions;
        private readonly bool _resolveLinkTos;
        private readonly ITimeProvider _timeProvider;

        private readonly HashSet<string> _eventsRequested = new HashSet<string>();
        private readonly Dictionary<string, long?> _preparePositions = new Dictionary<string, long?>();

        // event, link, progress
        private readonly Dictionary<string, Queue<Tuple<EventRecord, EventRecord, float>>> _buffers =
            new Dictionary<string, Queue<Tuple<EventRecord, EventRecord, float>>>();

        private const int _maxReadCount = 111;
        private long? _safePositionToJoin;
        private readonly Dictionary<string, bool> _eofs;
        private int _deliveredEvents;

        public MultiStreamEventReader(IPublisher publisher, Guid eventReaderCorrelationId, IPrincipal readAs, int phase, string[] streams, Dictionary<string, int> fromPositions, bool resolveLinkTos, ITimeProvider timeProvider, bool stopOnEof = false, int? stopAfterNEvents = null)
            : base(publisher, eventReaderCorrelationId, readAs, stopOnEof, stopAfterNEvents)
        {
            if (streams == null) throw new ArgumentNullException("streams");
            if (timeProvider == null) throw new ArgumentNullException("timeProvider");
            if (streams.Length == 0) throw new ArgumentException("streams");
            _streams = new HashSet<string>(streams);
            _eofs = _streams.ToDictionary(v => v, v => false);
            var positions = CheckpointTag.FromStreamPositions(phase, fromPositions);
            ValidateTag(positions);
            _fromPositions = positions;
            _resolveLinkTos = resolveLinkTos;
            _timeProvider = timeProvider;
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

        protected override void RequestEvents(bool delay)
        {
            if (PauseRequested || Paused)
                return;
            foreach (var stream in _streams)
                RequestEvents(stream, delay: delay);
        }

        protected override bool AreEventsRequested()
        {
            return _eventsRequested.Count != 0;
        }

        public void Handle(ClientMessage.ReadStreamEventsForwardCompleted message)
        {
            if (_disposed)
                return;
            if (!_streams.Contains(message.EventStreamId))
                throw new InvalidOperationException(string.Format("Invalid stream name: {0}", message.EventStreamId));
            if (!_eventsRequested.Contains(message.EventStreamId))
                throw new InvalidOperationException("Read events has not been requested");
            if (Paused)
                throw new InvalidOperationException("Paused");
            switch (message.Result)
            {
                case ReadStreamResult.NoStream:
                    _eofs[message.EventStreamId] = true;
                    UpdateSafePositionToJoin(message.EventStreamId, MessageToLastCommitPosition(message));
                    ProcessBuffers();
                    _eventsRequested.Remove(message.EventStreamId);
                    PauseOrContinueProcessing(delay: true);
                    CheckIdle();
                    CheckEof();
                    break;
                case ReadStreamResult.Success:
                    if (message.Events.Length == 0)
                    {
                        // the end
                        _eofs[message.EventStreamId] = true;
                        UpdateSafePositionToJoin(message.EventStreamId, MessageToLastCommitPosition(message));
                        CheckIdle();
                        CheckEof();
                    }
                    else
                    {
                        _eofs[message.EventStreamId] = false;
                        for (int index = 0; index < message.Events.Length; index++)
                        {
                            var @event = message.Events[index].Event;
                            var @link = message.Events[index].Link;
                            EventRecord positionEvent = (link ?? @event);
                            UpdateSafePositionToJoin(
                                positionEvent.EventStreamId, EventPairToPosition(message.Events[index]));
                            Queue<Tuple<EventRecord, EventRecord, float>> queue;
                            if (!_buffers.TryGetValue(positionEvent.EventStreamId, out queue))
                            {
                                queue = new Queue<Tuple<EventRecord, EventRecord, float>>();
                                _buffers.Add(positionEvent.EventStreamId, queue);
                            }
                            //TODO: progress calculation below is incorrect.  sum(current)/sum(last_event) where sum by all streams
                            queue.Enqueue(
                                Tuple.Create(
                                    @event, positionEvent, 100.0f*(link ?? @event).EventNumber/message.LastEventNumber));
                        }
                    }

                    ProcessBuffers();
                    _eventsRequested.Remove(message.EventStreamId);
                    PauseOrContinueProcessing(delay: message.Events.Length == 0);
                    break;
                case ReadStreamResult.AccessDenied:
                    SendNotAuthorized();
                    return;
                default:
                    throw new NotSupportedException(
                        string.Format("ReadEvents result code was not recognized. Code: {0}", message.Result));
            }
        }

        private void CheckEof()
        {
            if (_eofs.All(v => v.Value))
                SendEof();
        }

        private void CheckIdle()
        {
            if (_eofs.All(v => v.Value))
                _publisher.Publish(
                    new ReaderSubscriptionMessage.EventReaderIdle(EventReaderCorrelationId, _timeProvider.Now));
        }

        private void ProcessBuffers()
        {
            if (_safePositionToJoin == null)
                return;
            while (true)
            {
                var anyNonEmpty = false;
                var minStreamId = "";
                var any = false;
                var minPosition = GetMaxPosition();
                foreach (var buffer in _buffers)
                {
                    if (buffer.Value.Count == 0)
                        continue;
                    anyNonEmpty = true;
                    var head = buffer.Value.Peek();

                    var currentStreamId = buffer.Key;
                    var itemPosition = GetItemPosition(head);

                    if (_safePositionToJoin != null
                        && itemPosition.CompareTo(_safePositionToJoin.GetValueOrDefault()) <= 0
                        && itemPosition.CompareTo(minPosition) < 0)
                    {
                        minPosition = itemPosition;
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
                if (CheckEnough())
                    return;
                if (_buffers[minStreamId].Count == 0)
                    PauseOrContinueProcessing(delay: false);
            }
        }

        private bool CheckEnough()
        {
            if (_stopAfterNEvents != null && _deliveredEvents >= _stopAfterNEvents)
            {
                _publisher.Publish(new ReaderSubscriptionMessage.EventReaderEof(EventReaderCorrelationId, maxEventsReached: true));
                Dispose();
                return true;
            }
            return false;
        }

        private void RequestEvents(string stream, bool delay)
        {
            if (_disposed) throw new InvalidOperationException("Disposed");
            if (PauseRequested || Paused)
                throw new InvalidOperationException("Paused or pause requested");

            if (_eventsRequested.Contains(stream))
                return;
            Queue<Tuple<EventRecord, EventRecord, float>> queue;
            if (_buffers.TryGetValue(stream, out queue) && queue.Count > 0)
                return;
            _eventsRequested.Add(stream);

            var readEventsForward = new ClientMessage.ReadStreamEventsForward(
                Guid.NewGuid(), EventReaderCorrelationId, new SendToThisEnvelope(this), stream, _fromPositions.Streams[stream],
                _maxReadCount, _resolveLinkTos, false, null, ReadAs);
            if (delay)
                _publisher.Publish(
                    TimerMessage.Schedule.Create(
                        TimeSpan.FromMilliseconds(250), new PublishEnvelope(_publisher, crossThread: true),
                        readEventsForward));
            else
                _publisher.Publish(readEventsForward);
        }

        private void DeliverSafePositionToJoin()
        {
            if (_stopOnEof || _stopAfterNEvents != null || _safePositionToJoin == null)
                return;
            // deliver if already available
            _publisher.Publish(
                new ReaderSubscriptionMessage.CommittedEventDistributed(
                    EventReaderCorrelationId, null, PositionToSafeJoinPosition(_safePositionToJoin), 100.0f, source: this.GetType()));
        }

        private void UpdateSafePositionToJoin(string streamId, long? preparePosition)
        {
            _preparePositions[streamId] = preparePosition;
            if (_preparePositions.All(v => v.Value != null))
                _safePositionToJoin = _preparePositions.Min(v => v.Value.GetValueOrDefault());
        }

        private void DeliverEvent(EventRecord @event, EventRecord positionEvent, float progress)
        {
            _deliveredEvents ++;
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
                new ReaderSubscriptionMessage.CommittedEventDistributed(
                    EventReaderCorrelationId,
                    new ResolvedEvent(
                        streamId, positionEvent.EventNumber, @event.EventStreamId, @event.EventNumber, resolvedLinkTo,
                        new TFPos(-1, positionEvent.LogPosition), new TFPos(-1, @event.LogPosition), @event.EventId,
                        @event.EventType, (@event.Flags & PrepareFlags.IsJson) != 0, @event.Data, @event.Metadata,
                        @event == positionEvent ? null : positionEvent.Metadata, positionEvent.TimeStamp),
                    _stopOnEof ? (long?) null : positionEvent.LogPosition, progress, source: this.GetType()));
        }

        private long? EventPairToPosition(EventStore.Core.Data.ResolvedEvent resolvedEvent)
        {
            return resolvedEvent.OriginalEvent.LogPosition;
        }

        private long? MessageToLastCommitPosition(ClientMessage.ReadStreamEventsForwardCompleted message)
        {
            return GetLastCommitPositionFrom(message);
        }

        private long GetItemPosition(Tuple<EventRecord, EventRecord, float> head)
        {
            return head.Item2.LogPosition;
        }

        private long GetMaxPosition()
        {
            return long.MaxValue;
        }

        private long? PositionToSafeJoinPosition(long? safePositionToJoin)
        {
            return safePositionToJoin;
        }
    }
}
