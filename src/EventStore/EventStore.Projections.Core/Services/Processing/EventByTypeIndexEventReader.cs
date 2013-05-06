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
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.UserManagement;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class EventByTypeIndexEventReader : EventReader, IHandle<ClientMessage.ReadStreamEventsForwardCompleted>,
        IHandle<ClientMessage.ReadStreamEventsBackwardCompleted>, IHandle<ClientMessage.ReadAllEventsForwardCompleted>
    {
        private const int _maxReadCount = 111;
        private readonly HashSet<string> _eventTypes;
        private readonly Dictionary<string, string> _streamToEventType;
        private readonly bool _resolveLinkTos;
        private readonly ITimeProvider _timeProvider;
        private readonly HashSet<string> _eventsRequested = new HashSet<string>();

        private bool _indexCheckpointStreamRequested = false;
        private int _lastKnownIndexCheckpointEventNumber = -1;
        private TFPos _lastKnownIndexCheckpointPosition = default(TFPos);

        protected class PendingEvent
        {
            public readonly EventRecord Event;
            public readonly EventRecord PositionEvent;
            public readonly float Progress;
            public readonly TFPos TfPosition;

            public PendingEvent(EventRecord @event, EventRecord positionEvent, TFPos tfPosition, float progress)
            {
                Event = @event;
                PositionEvent = positionEvent;
                Progress = progress;
                TfPosition = tfPosition;
            }
        }

        private readonly Dictionary<string, Queue<PendingEvent>> _buffers =
            new Dictionary<string, Queue<PendingEvent>>();

        private readonly Dictionary<string, bool> _eofs;
        private int _deliveredEvents;
        private bool _readAllMode = false;
        private bool _tfEventsRequested;
        private readonly Dictionary<string, int> _fromPositions;
        private TFPos _fromTfPosition;
        private TFPos _lastEventPosition;

        public EventByTypeIndexEventReader(
            IPublisher publisher, Guid eventReaderCorrelationId, string[] eventTypes, TFPos fromTfPosition, Dictionary<string, int> fromPositions,
            bool resolveLinkTos, ITimeProvider timeProvider, bool stopOnEof = false, int? stopAfterNEvents = null)
            : base(
                publisher, eventReaderCorrelationId, stopOnEof,
                stopAfterNEvents)
        {
            if (eventTypes == null) throw new ArgumentNullException("eventTypes");
            if (timeProvider == null) throw new ArgumentNullException("timeProvider");
            if (eventTypes.Length == 0) throw new ArgumentException("empty", "eventTypes");

            _eventTypes = new HashSet<string>(eventTypes);
            _streamToEventType = eventTypes.ToDictionary(v => "$et-" + v, v => v);

            _eofs = _streamToEventType.Keys.ToDictionary(v => v, v => false);
            ValidateTag(fromPositions);
            _fromPositions = fromPositions;
            _fromTfPosition = fromTfPosition;
            // whatever the first event returned is (even if we start from the same position as the last processed event
            // let subscription handle this 
            _lastEventPosition = new TFPos(0, -10); 
            _resolveLinkTos = resolveLinkTos;
            _timeProvider = timeProvider;
        }

        protected TFPos? EventPairToPosition(EventStore.Core.Data.ResolvedEvent resolvedEvent)
        {
            var @link = resolvedEvent.Link;
            // we assume that event index was written by a standard projection with fromAll() source 
            // and therefore full event position can be recovered from the checkpoint tag
            return @link.Metadata.ParseCheckpointTagJson(default(ProjectionVersion)).Tag.Position;
        }

        protected TFPos? MessageToLastCommitPosition(
            ClientMessage.ReadStreamEventsForwardCompleted message)
        {
            var lastCommitPosition = GetLastCommitPositionFrom(message);
            return lastCommitPosition.HasValue ? new TFPos(message.LastCommitPosition, 0) : (TFPos?)null;
        }

        protected TFPos GetTargetEventPosition(PendingEvent head)
        {
            return head.TfPosition;
        }

        protected long? PositionToSafeJoinPosition(TFPos? safePositionToJoin)
        {
            return safePositionToJoin != null ? safePositionToJoin.GetValueOrDefault().CommitPosition : (long?) null;
        }

        private void ValidateTag(Dictionary<string, int> fromPositions)
        {
            if (_eventTypes.Count != fromPositions.Count)
                throw new ArgumentException("Number of streams does not match", "fromPositions");

            foreach (var stream in _streamToEventType.Keys.Where(stream => !fromPositions.ContainsKey(stream)))
            {
                throw new ArgumentException(
                    string.Format("The '{0}' stream position has not been set", stream), "fromPositions");
            }
        }

        protected override string FromAsText()
        {
            return _fromPositions.ToString();
        }

        protected override void RequestEvents()
        {
            RequestEventsAll();
        }

        protected override bool AreEventsRequested()
        {
            return _eventsRequested.Count != 0 || _indexCheckpointStreamRequested || _tfEventsRequested;
        }

        public void Handle(ClientMessage.ReadStreamEventsForwardCompleted message)
        {
            if (_disposed || _readAllMode)
                return;
            if (message.EventStreamId == "$et")
            {
                ReadIndexCheckpointStreamCompleted(message.Result, message.Events);
                return;
            }

            if (!_streamToEventType.ContainsKey(message.EventStreamId))
                throw new InvalidOperationException(string.Format("Invalid stream name: {0}", message.EventStreamId));
            if (!_eventsRequested.Contains(message.EventStreamId))
                throw new InvalidOperationException("Read events has not been requested");
            if (_paused)
                throw new InvalidOperationException("Paused");
            _eventsRequested.Remove(message.EventStreamId);
            switch (message.Result)
            {
                case ReadStreamResult.NoStream:
                    _eofs[message.EventStreamId] = true;
                    ProcessBuffers();
                    PauseOrContinueReadingStream(message.EventStreamId, delay: true);
                    CheckSwitch();
                    break;
                case ReadStreamResult.Success:
                    UpdateNextStreamPosition(message.EventStreamId, message.NextEventNumber);
                    var isEof = message.Events.Length == 0;
                    if (isEof)
                    {
                        // the end
                        _eofs[message.EventStreamId] = true;
                    }
                    else
                    {
                        _eofs[message.EventStreamId] = false;
                        EnqueueEvents(message);
                    }
                    ProcessBuffers();
                    //TODO: IT MUST REQUEST EVENTS FROM NEW POSITION (CURRENTLY OLD)
                    PauseOrContinueReadingStream(message.EventStreamId, delay: isEof);
                    CheckSwitch();
                    break;
                default:
                    throw new NotSupportedException(
                        string.Format("ReadEvents result code was not recognized. Code: {0}", message.Result));
            }
        }

        private void EnqueueEvents(ClientMessage.ReadStreamEventsForwardCompleted message)
        {
            for (int index = 0; index < message.Events.Length; index++)
            {
                var @event = message.Events[index].Event;
                var @link = message.Events[index].Link;
                EventRecord positionEvent = (link ?? @event);
                var queue = GetStreamQueue(positionEvent);
                //TODO: progress calculation below is incorrect.  sum(current)/sum(last_event) where sum by all streams
                var tfPosition = positionEvent.Metadata.ParseCheckpointTagJson(default(ProjectionVersion)).Tag.Position;
                var progress = 100.0f*(link ?? @event).EventNumber/message.LastEventNumber;
                var pendingEvent = new PendingEvent(@event, positionEvent, tfPosition, progress);
                queue.Enqueue(pendingEvent);
            }
        }

        private Queue<PendingEvent> GetStreamQueue(EventRecord positionEvent)
        {
            Queue<PendingEvent> queue;
            if (!_buffers.TryGetValue(positionEvent.EventStreamId, out queue))
            {
                queue = new Queue<PendingEvent>();
                _buffers.Add(positionEvent.EventStreamId, queue);
            }
            return queue;
        }

        private void CheckSwitch()
        {
            if (_disposed) // max N reached
                return;
            Queue<PendingEvent> q;
            if (
                _streamToEventType.Keys.All(
                    v =>
                    _eofs[v]
                    || _buffers.TryGetValue(v, out q) && q.Count > 0 && !IsIndexedTfPosition(q.Peek().TfPosition)))
            {
                _readAllMode = true;
                RequestEventsAll();
            }
        }

        private bool IsIndexedTfPosition(TFPos tfPosition)
        {
            //TODO: ensure <= is acceptable and replace
            return tfPosition < _lastKnownIndexCheckpointPosition;
        }

        private void PauseOrContinueReadingStream(string eventStreamId, bool delay)
        {
            if (_disposed) // max N reached
                return;
            if (_pauseRequested)
                _paused = !AreEventsRequested();
            else
                RequestEvents(eventStreamId, delay);
            _publisher.Publish(CreateTickMessage());
        }

        private void ReadIndexCheckpointStreamCompleted(ReadStreamResult result, EventStore.Core.Data.ResolvedEvent[] events)
        {
            if (_disposed)
                return;
            if (_readAllMode)
                throw new InvalidOperationException();

            if (!_indexCheckpointStreamRequested)
                throw new InvalidOperationException("Read index checkpoint has not been requested");
            if (_paused)
                throw new InvalidOperationException("Paused");
            _indexCheckpointStreamRequested = false;
            switch (result)
            {
                case ReadStreamResult.NoStream:
                    if (_pauseRequested)
                        _paused = !AreEventsRequested();
                    else
                        RequestCheckpointStream(delay: true);
                    _publisher.Publish(CreateTickMessage());
                    break;
                case ReadStreamResult.Success:
                    if (events.Length != 0)
                    {
                        //NOTE: only one event if backward order was requested
                        foreach (var @event in events)
                        {
                            var data = @event.Event.Data.ParseCheckpointTagJson(default(ProjectionVersion)).Tag;
                            _lastKnownIndexCheckpointPosition = data.Position;
                            _lastKnownIndexCheckpointEventNumber = @event.Event.EventNumber;
                        }
                    }
                    if (_disposed)
                        return;

                    if (_pauseRequested)
                        _paused = !AreEventsRequested();
                    else if (events.Length == 0)
                        RequestCheckpointStream(delay: true);
                    _publisher.Publish(CreateTickMessage());
                    break;
                default:
                    throw new NotSupportedException(
                        string.Format("ReadEvents result code was not recognized. Code: {0}", result));
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
            if (_disposed) // max N reached
                return;
            if (_readAllMode)
                throw new InvalidOperationException();
            while (true)
            {
                var minStreamId = "";
                var minPosition = new TFPos(long.MaxValue, long.MaxValue);
                var any = false;
                var anyEof = false;
                foreach (var streamId in _streamToEventType.Keys)
                {
                    Queue<PendingEvent> buffer;
                    _buffers.TryGetValue(streamId, out buffer);

                    if ((buffer == null || buffer.Count == 0))
                        if (_eofs[streamId])
                        {
                            anyEof = true;
                            continue; // eof - will check if it was safe later
                        }
                        else
                            return; // still reading

                    var head = buffer.Peek();
                    var targetEventPosition = GetTargetEventPosition(head);

                    if (targetEventPosition < minPosition)
                    {
                        minPosition = targetEventPosition;
                        minStreamId = streamId;
                        any = true;
                    }
                }

                if (!any)
                    break;

                if (!anyEof || IsIndexedTfPosition(minPosition))
                {
                    var minHead = _buffers[minStreamId].Dequeue();
                    DeliverEventRetrievedByIndex(minHead.Event, minHead.PositionEvent, minHead.Progress, minPosition);
                }
                else
                    return; // no safe events to deliver

                if (CheckEnough())
                    return;
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

        private void RequestEventsAll()
        {
            if (_pauseRequested || _paused)
                return;
            if (_readAllMode)
            {
                RequestTfEvents(delay: false);
            }
            else
            {
                foreach (var stream in _streamToEventType.Keys)
                    RequestEvents(stream, delay: false);
                RequestCheckpointStream(delay: false);
            }
        }

        private void RequestTfEvents(bool delay)
        {
            if (_disposed || !_readAllMode) throw new InvalidOperationException("Disposed or invalid mode");
            if (_pauseRequested || _paused)
                throw new InvalidOperationException("Paused or pause requested");
            if (_tfEventsRequested)
                return;

            _tfEventsRequested = true;
            //TODO: we do not need resolve links, but lets check first with
            var readRequest = new ClientMessage.ReadAllEventsForward(
                EventReaderCorrelationId, new SendToThisEnvelope(this), _fromTfPosition.CommitPosition,
                _fromTfPosition.PreparePosition == -1 ? 0 : _fromTfPosition.PreparePosition, 111, true, null, SystemAccount.Principal);
            PublishIORequest(delay, readRequest);
        }

        private void RequestCheckpointStream(bool delay)
        {
            if (_disposed || _readAllMode) throw new InvalidOperationException("Disposed or invalid mode");
            if (_pauseRequested || _paused)
                throw new InvalidOperationException("Paused or pause requested");
            if (_indexCheckpointStreamRequested)
                return;

            _indexCheckpointStreamRequested = true;

            Message readRequest;
            if (_lastKnownIndexCheckpointEventNumber == -1)
            {
                readRequest = new ClientMessage.ReadStreamEventsBackward(
                    EventReaderCorrelationId, new SendToThisEnvelope(this), "$et", -1, 1, false, null,
                    SystemAccount.Principal);
            }
            else
            {
                readRequest = new ClientMessage.ReadStreamEventsForward(
                    EventReaderCorrelationId, new SendToThisEnvelope(this), "$et", _lastKnownIndexCheckpointEventNumber + 1,
                    100, false, null, SystemAccount.Principal);
            }
            PublishIORequest(delay, readRequest);
        }

        private void RequestEvents(string stream, bool delay)
        {
            if (_disposed || _readAllMode) throw new InvalidOperationException("Disposed or invalid mode");
            if (_pauseRequested || _paused)
                throw new InvalidOperationException("Paused or pause requested");

            if (_eventsRequested.Contains(stream))
                return;
            Queue<PendingEvent> queue;
            if (_buffers.TryGetValue(stream, out queue) && queue.Count > 0)
                return;
            _eventsRequested.Add(stream);

            var readEventsForward = new ClientMessage.ReadStreamEventsForward(
                EventReaderCorrelationId, new SendToThisEnvelope(this), stream,
                _fromPositions[stream], _maxReadCount, _resolveLinkTos, null,
                SystemAccount.Principal);
            PublishIORequest(delay, readEventsForward);
        }

        private void PublishIORequest(bool delay, Message readEventsForward)
        {
            if (delay)
                _publisher.Publish(
                    TimerMessage.Schedule.Create(
                        TimeSpan.FromMilliseconds(250), new PublishEnvelope(_publisher, crossThread: true),
                        readEventsForward));
            else
                _publisher.Publish(readEventsForward);
        }

        private void DeliverLastCommitPosition(TFPos lastPosition)
        {
            if (_stopOnEof || _stopAfterNEvents != null)
                return;
            _publisher.Publish(
                new ReaderSubscriptionMessage.CommittedEventDistributed(
                    EventReaderCorrelationId, null, lastPosition.PreparePosition, 100.0f));
                //TODO: check was is passed here
        }

        private void DeliverEventRetrievedByIndex(
            EventRecord @event, EventRecord positionEvent, float progress, TFPos position)
        {
            if (position <= _lastEventPosition)
                return;
            _fromTfPosition = position;
            _lastEventPosition = position;
            _deliveredEvents ++;
            string streamId = positionEvent.EventStreamId;
            //TODO: add event sequence validation for inside the index stream
            _publisher.Publish(
                //TODO: publish both link and event data
                new ReaderSubscriptionMessage.CommittedEventDistributed(
                    EventReaderCorrelationId,
                    new ResolvedEvent(
                        streamId, positionEvent.EventNumber, @event.EventStreamId, @event.EventNumber, true, position,
                        @event.EventId, @event.EventType, (@event.Flags & PrepareFlags.IsJson) != 0, @event.Data,
                        @event.Metadata, positionEvent.Metadata, positionEvent.TimeStamp),
                    _stopOnEof ? (long?) null : positionEvent.LogPosition, progress));
        }

        private void DeliverEventRetrievedFromTf(EventRecord @event, float progress, TFPos position)
        {
            if (position <= _lastEventPosition)
                return;
            _lastEventPosition = position;
            _deliveredEvents ++;
            _publisher.Publish(
                //TODO: publish both link and event data
                new ReaderSubscriptionMessage.CommittedEventDistributed(
                    EventReaderCorrelationId,
                    new ResolvedEvent(
                        @event.EventStreamId, @event.EventNumber, @event.EventStreamId, @event.EventNumber, false, position,
                        @event.EventId, @event.EventType, (@event.Flags & PrepareFlags.IsJson) != 0, @event.Data,
                        @event.Metadata, null, @event.TimeStamp),
                    _stopOnEof ? (long?) null : position.PreparePosition, progress));
        }

        public void Handle(ClientMessage.ReadStreamEventsBackwardCompleted message)
        {
            if (_disposed || _readAllMode)
                return;
            ReadIndexCheckpointStreamCompleted(message.Result, message.Events);
        }

        public void Handle(ClientMessage.ReadAllEventsForwardCompleted message)
        {
            if (_disposed)
                return;
            if (!_readAllMode)
                throw new InvalidOperationException();

            if (!_tfEventsRequested)
                throw new InvalidOperationException("TF events has not been requested");
            if (_paused)
                throw new InvalidOperationException("Paused");
            _tfEventsRequested = false;
            switch (message.Result)
            {
                case ReadAllResult.Success:
                    var eof = message.Events.Length == 0;
                    var willDispose = _stopOnEof && eof;
                    _fromTfPosition = message.NextPos;

                    if (!willDispose)
                    {
                        if (_pauseRequested)
                            _paused = true; // !AreEventsRequested(); -- we are the only reader
                        else if (eof)
                            RequestTfEvents(delay: true);
                        else
                            RequestEvents();
                    }

                    if (eof)
                    {
                        // the end
                        //TODO: is it safe to pass NEXT as last commit position here
                         DeliverLastCommitPosition(message.NextPos);
                        // allow joining heading distribution
                        SendIdle();
                        SendEof();
                    }
                    else
                    {
                        foreach (var @event in message.Events)
                        {
                            var byStream = @event.Link != null
                                           && _streamToEventType.ContainsKey(@event.Link.EventStreamId);
                            var byEvent = @event.Link == null && _eventTypes.Contains(@event.Event.EventType);
                            if (byStream) // ignore data just update positions
                                UpdateNextStreamPosition(@event.Link.EventStreamId, @event.Link.EventNumber + 1);
                            else if (byEvent)
                            {
                                DeliverEventRetrievedFromTf(@event.Event, 100.0f*@event.Event.LogPosition/message.TfEofPosition, @event.OriginalPosition.Value);
                            }
                            if (CheckEnough())
                                return;

                        }
                    }
                    if (_disposed)
                        return;

                    _publisher.Publish(CreateTickMessage());
                    break;
                default:
                    throw new NotSupportedException(
                        string.Format("ReadEvents result code was not recognized. Code: {0}", message.Result));
            }
        }

        private void UpdateNextStreamPosition(string eventStreamId, int nextPosition)
        {
            int streamPosition;
            if (!_fromPositions.TryGetValue(eventStreamId, out streamPosition))
                streamPosition = -1;
            if (nextPosition > streamPosition)
                _fromPositions[eventStreamId] = nextPosition;
        }

        private void SendIdle()
        {
            _publisher.Publish(
                new ReaderSubscriptionMessage.EventReaderIdle(EventReaderCorrelationId, _timeProvider.Now));
        }


    }
}
