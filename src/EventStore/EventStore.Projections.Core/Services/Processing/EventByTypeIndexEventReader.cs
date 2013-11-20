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
    public class EventByTypeIndexEventReader : EventReader
    {
        private const int _maxReadCount = 111;
        private readonly HashSet<string> _eventTypes;
        private readonly bool _resolveLinkTos;
        private readonly ITimeProvider _timeProvider;

        private class PendingEvent
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

        private int _deliveredEvents;
        private State _state;
        private TFPos _lastEventPosition;
        private readonly Dictionary<string, int> _fromPositions;
        private readonly Dictionary<string, string> _streamToEventType;

        public EventByTypeIndexEventReader(
            IPublisher publisher, Guid eventReaderCorrelationId, IPrincipal readAs, string[] eventTypes,
            TFPos fromTfPosition, Dictionary<string, int> fromPositions, bool resolveLinkTos, ITimeProvider timeProvider,
            bool stopOnEof = false, int? stopAfterNEvents = null)
            : base(publisher, eventReaderCorrelationId, readAs, stopOnEof, stopAfterNEvents)
        {
            if (eventTypes == null) throw new ArgumentNullException("eventTypes");
            if (timeProvider == null) throw new ArgumentNullException("timeProvider");
            if (eventTypes.Length == 0) throw new ArgumentException("empty", "eventTypes");

            _timeProvider = timeProvider;
            _eventTypes = new HashSet<string>(eventTypes);
            _streamToEventType = eventTypes.ToDictionary(v => "$et-" + v, v => v);
            _lastEventPosition = fromTfPosition;
            _resolveLinkTos = resolveLinkTos;

            ValidateTag(fromPositions);

            _fromPositions = fromPositions;
            _state = new IndexBased(_eventTypes, this, readAs);
        }

        private void ValidateTag(Dictionary<string, int> fromPositions)
        {
            if (_eventTypes.Count != fromPositions.Count)
                throw new ArgumentException("Number of streams does not match", "fromPositions");

            foreach (var stream in _streamToEventType.Keys.Where(stream => !fromPositions.ContainsKey(stream)))
            {
                throw new ArgumentException(
                    String.Format("The '{0}' stream position has not been set", stream), "fromPositions");
            }
        }


        public override void Dispose()
        {
            _state.Dispose();
            base.Dispose();
        }

        protected override void RequestEvents(bool delay)
        {
            if (_disposed || PauseRequested || Paused)
                return;
            _state.RequestEvents(delay);
        }

        protected override bool AreEventsRequested()
        {
            return _state.AreEventsRequested();
        }

        private bool CheckEnough()
        {
            if (_stopAfterNEvents != null && _deliveredEvents >= _stopAfterNEvents)
            {
                _publisher.Publish(
                    new ReaderSubscriptionMessage.EventReaderEof(EventReaderCorrelationId, maxEventsReached: true));
                Dispose();
                return true;
            }
            return false;
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

        private void UpdateNextStreamPosition(string eventStreamId, int nextPosition)
        {
            int streamPosition;
            if (!_fromPositions.TryGetValue(eventStreamId, out streamPosition))
                streamPosition = -1;
            if (nextPosition > streamPosition)
                _fromPositions[eventStreamId] = nextPosition;
        }

        private abstract class State : IDisposable
        {
            public abstract void RequestEvents(bool delay);
            public abstract bool AreEventsRequested();
            public abstract void Dispose();

            protected readonly EventByTypeIndexEventReader _reader;
            protected readonly IPrincipal _readAs;

            protected State(EventByTypeIndexEventReader reader, IPrincipal readAs)
            {
                _reader = reader;
                _readAs = readAs;
            }

            protected void DeliverEvent(float progress, ResolvedEvent resolvedEvent, TFPos position)
            {
                if (resolvedEvent.OriginalPosition <= _reader._lastEventPosition)
                    return;
                _reader._lastEventPosition = resolvedEvent.OriginalPosition;
                _reader._deliveredEvents ++;
                _reader._publisher.Publish(
                    //TODO: publish both link and event data
                    new ReaderSubscriptionMessage.CommittedEventDistributed(
                        _reader.EventReaderCorrelationId, resolvedEvent,
                        _reader._stopOnEof ? (long?) null : position.PreparePosition, progress, source: this.GetType()));
            }

            protected void SendNotAuthorized()
            {
                _reader.SendNotAuthorized();
            }
        }

        private class IndexBased : State,
                                   IHandle<ClientMessage.ReadStreamEventsForwardCompleted>,
                                   IHandle<ClientMessage.ReadStreamEventsBackwardCompleted>

        {
            private readonly Dictionary<string, string> _streamToEventType;
            private readonly HashSet<string> _eventsRequested = new HashSet<string>();
            private bool _indexCheckpointStreamRequested;
            private int _lastKnownIndexCheckpointEventNumber = -1;
            private TFPos? _lastKnownIndexCheckpointPosition = null;

            private readonly Dictionary<string, Queue<PendingEvent>> _buffers =
                new Dictionary<string, Queue<PendingEvent>>();

            private readonly Dictionary<string, bool> _eofs;
            private bool _disposed;
            private readonly IPublisher _publisher;

            public IndexBased(HashSet<string> eventTypes, EventByTypeIndexEventReader reader, IPrincipal readAs)
                : base(reader, readAs)
            {
                _streamToEventType = eventTypes.ToDictionary(v => "$et-" + v, v => v);
                _eofs = _streamToEventType.Keys.ToDictionary(v => v, v => false);
                // whatever the first event returned is (even if we start from the same position as the last processed event
                // let subscription handle this 
                _publisher = _reader._publisher;
            }


            private TFPos GetTargetEventPosition(PendingEvent head)
            {
                return head.TfPosition;
            }

            public void Handle(ClientMessage.ReadStreamEventsForwardCompleted message)
            {
                if (_disposed)
                    return;
                if (message.Result == ReadStreamResult.AccessDenied)
                {
                    SendNotAuthorized();
                    return;
                }
                if (message.EventStreamId == "$et")
                {
                    ReadIndexCheckpointStreamCompleted(message.Result, message.Events);
                    return;
                }

                if (!_streamToEventType.ContainsKey(message.EventStreamId))
                    throw new InvalidOperationException(
                        String.Format("Invalid stream name: {0}", message.EventStreamId));
                if (!_eventsRequested.Contains(message.EventStreamId))
                    throw new InvalidOperationException("Read events has not been requested");
                if (_reader.Paused)
                    throw new InvalidOperationException("Paused");
                switch (message.Result)
                {
                    case ReadStreamResult.NoStream:
                        _eofs[message.EventStreamId] = true;
                        ProcessBuffersAndContinue(message, eof: true);
                        break;
                    case ReadStreamResult.Success:
                        _reader.UpdateNextStreamPosition(message.EventStreamId, message.NextEventNumber);
                        var isEof = message.Events.Length == 0;
                        _eofs[message.EventStreamId] = isEof;
                        EnqueueEvents(message);
                        ProcessBuffersAndContinue(message, eof: isEof);
                        break;
                    default:
                        throw new NotSupportedException(
                            String.Format("ReadEvents result code was not recognized. Code: {0}", message.Result));
                }
            }

            private void ProcessBuffersAndContinue(ClientMessage.ReadStreamEventsForwardCompleted message, bool eof)
            {
                ProcessBuffers();
                _eventsRequested.Remove(message.EventStreamId);
                _reader.PauseOrContinueProcessing(delay: eof);
                CheckSwitch();
            }

            private void CheckSwitch()
            {
                if (ShouldSwitch())
                {
                    Dispose();
                    _reader.DoSwitch(_lastKnownIndexCheckpointPosition.Value);
                }
            }

            public void Handle(ClientMessage.ReadStreamEventsBackwardCompleted message)
            {
                if (_disposed)
                    return;
                if (message.Result == ReadStreamResult.AccessDenied)
                {
                    SendNotAuthorized();
                    return;
                }
                ReadIndexCheckpointStreamCompleted(message.Result, message.Events);
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
                    var tfPosition =
                        positionEvent.Metadata.ParseCheckpointTagJson().Position;
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

            private bool BeforeTheLastKnownIndexCheckpoint(TFPos tfPosition)
            {
                return _lastKnownIndexCheckpointPosition != null && tfPosition <= _lastKnownIndexCheckpointPosition;
            }

            private void ReadIndexCheckpointStreamCompleted(
                ReadStreamResult result, EventStore.Core.Data.ResolvedEvent[] events)
            {
                if (_disposed)
                    return;

                if (!_indexCheckpointStreamRequested)
                    throw new InvalidOperationException("Read index checkpoint has not been requested");
                if (_reader.Paused)
                    throw new InvalidOperationException("Paused");
                _indexCheckpointStreamRequested = false;
                switch (result)
                {
                    case ReadStreamResult.NoStream:
                        _reader.PauseOrContinueProcessing(delay: true);
                        _lastKnownIndexCheckpointPosition = default(TFPos);
                        break;
                    case ReadStreamResult.Success:
                        if (events.Length == 0)
                        {
                            if (_lastKnownIndexCheckpointPosition == null)
                                _lastKnownIndexCheckpointPosition = default(TFPos);
                        }
                        else
                        {
                            //NOTE: only one event if backward order was requested
                            foreach (var @event in events)
                            {
                                var data = @event.Event.Data.ParseCheckpointTagJson();
                                _lastKnownIndexCheckpointEventNumber = @event.Event.EventNumber;
                                _lastKnownIndexCheckpointPosition = data.Position;
                                // reset eofs before this point - probably some where updated so we cannot go 
                                // forward with this position without making sure nothing appeared
                                // NOTE: performance is not very good, but we should switch to TF mode shortly
                                foreach (var key in _eofs.Keys.ToArray())
                                    _eofs[key] = false;
                            }
                        }
                        _reader.PauseOrContinueProcessing(delay: events.Length == 0);
                        break;
                    default:
                        throw new NotSupportedException(
                            String.Format("ReadEvents result code was not recognized. Code: {0}", result));
                }
            }

            private void ProcessBuffers()
            {
                if (_disposed) // max N reached
                    return;
                while (true)
                {
                    var minStreamId = "";
                    var minPosition = new TFPos(Int64.MaxValue, Int64.MaxValue);
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

                    if (!anyEof || BeforeTheLastKnownIndexCheckpoint(minPosition))
                    {
                        var minHead = _buffers[minStreamId].Dequeue();
                        DeliverEventRetrievedByIndex(
                            minHead.Event, minHead.PositionEvent, minHead.Progress, minPosition);
                    }
                    else
                        return; // no safe events to deliver

                    if (_reader.CheckEnough())
                        return;

                    if (_buffers[minStreamId].Count == 0)
                        _reader.PauseOrContinueProcessing(delay: false);
                }
            }

            private void RequestCheckpointStream(bool delay)
            {
                if (_disposed)
                    throw new InvalidOperationException("Disposed");
                if (_reader.PauseRequested || _reader.Paused)
                    throw new InvalidOperationException("Paused or pause requested");
                if (_indexCheckpointStreamRequested)
                    return;

                _indexCheckpointStreamRequested = true;

                Message readRequest;
                if (_lastKnownIndexCheckpointEventNumber == -1)
                {
                    readRequest = new ClientMessage.ReadStreamEventsBackward(
                        Guid.NewGuid(), _reader.EventReaderCorrelationId, new SendToThisEnvelope(this), "$et", -1, 1, false, false, null,
                        _readAs);
                }
                else
                {
                    readRequest = new ClientMessage.ReadStreamEventsForward(
                        Guid.NewGuid(), _reader.EventReaderCorrelationId, new SendToThisEnvelope(this), "$et",
                        _lastKnownIndexCheckpointEventNumber + 1, 100, false, false, null, _readAs);
                }
                _reader.PublishIORequest(delay, readRequest);
            }

            private void RequestEvents(string stream, bool delay)
            {
                if (_disposed)
                    throw new InvalidOperationException("Disposed");
                if (_reader.PauseRequested || _reader.Paused)
                    throw new InvalidOperationException("Paused or pause requested");

                if (_eventsRequested.Contains(stream))
                    return;
                Queue<PendingEvent> queue;
                if (_buffers.TryGetValue(stream, out queue) && queue.Count > 0)
                    return;
                _eventsRequested.Add(stream);

                var readEventsForward = new ClientMessage.ReadStreamEventsForward(
                    Guid.NewGuid(), _reader.EventReaderCorrelationId, new SendToThisEnvelope(this), stream,
                    _reader._fromPositions[stream], _maxReadCount, _reader._resolveLinkTos, false, null,
                    _readAs);
                _reader.PublishIORequest(delay, readEventsForward);
            }

            private void DeliverEventRetrievedByIndex(
                EventRecord @event, EventRecord positionEvent, float progress, TFPos position)
            {
                //TODO: add event sequence validation for inside the index stream
                var resolvedEvent = new ResolvedEvent(
                    positionEvent.EventStreamId, positionEvent.EventNumber, @event.EventStreamId, @event.EventNumber,
                    true, new TFPos(-1, positionEvent.LogPosition), position, @event.EventId, @event.EventType,
                    (@event.Flags & PrepareFlags.IsJson) != 0, @event.Data, @event.Metadata, positionEvent.Metadata,
                    positionEvent.TimeStamp);
                DeliverEvent(progress, resolvedEvent, position);
            }

            public override bool AreEventsRequested()
            {
                return _eventsRequested.Count != 0 || _indexCheckpointStreamRequested;
            }

            public override void Dispose()
            {
                _disposed = true;
            }

            public override void RequestEvents(bool delay)
            {
                foreach (var stream in _streamToEventType.Keys)
                    RequestEvents(stream, delay: delay);
                RequestCheckpointStream(delay: delay);
            }

            private bool ShouldSwitch()
            {
                if (_disposed)
                    return false;
                if (_reader.Paused || _reader.PauseRequested)
                    return false;
                Queue<PendingEvent> q;
                var shouldSwitch = _lastKnownIndexCheckpointPosition != null
                                   && _streamToEventType.Keys.All(
                                       v =>
                                       _eofs[v]
                                       || _buffers.TryGetValue(v, out q) && q.Count > 0
                                       && !BeforeTheLastKnownIndexCheckpoint(q.Peek().TfPosition));
                return shouldSwitch;
            }
        }

        private class TfBased : State, IHandle<ClientMessage.ReadAllEventsForwardCompleted>
        {
            private readonly HashSet<string> _eventTypes;
            private readonly ITimeProvider _timeProvider;
            private bool _tfEventsRequested;
            private bool _disposed;
            private readonly Dictionary<string, string> _streamToEventType;
            private readonly IPublisher _publisher;
            private TFPos _fromTfPosition;

            public TfBased(
                ITimeProvider timeProvider, EventByTypeIndexEventReader reader, TFPos fromTfPosition,
                IPublisher publisher, IPrincipal readAs)
                : base(reader, readAs)
            {
                _timeProvider = timeProvider;
                _eventTypes = reader._eventTypes;
                _streamToEventType = _eventTypes.ToDictionary(v => "$et-" + v, v => v);
                _publisher = publisher;
                _fromTfPosition = fromTfPosition;
            }

            public void Handle(ClientMessage.ReadAllEventsForwardCompleted message)
            {
                if (_disposed)
                    return;
                if (message.Result == ReadAllResult.AccessDenied)
                {
                    SendNotAuthorized();
                    return;
                }

                if (!_tfEventsRequested)
                    throw new InvalidOperationException("TF events has not been requested");
                if (_reader.Paused)
                    throw new InvalidOperationException("Paused");
                _tfEventsRequested = false;
                switch (message.Result)
                {
                    case ReadAllResult.Success:
                        var eof = message.Events.Length == 0;
                        var willDispose = _reader._stopOnEof && eof;
                        _fromTfPosition = message.NextPos;

                        if (!willDispose)
                        {
                            _reader.PauseOrContinueProcessing(delay: eof);
                        }

                        if (eof)
                        {
                            // the end
                            //TODO: is it safe to pass NEXT as last commit position here
                            DeliverLastCommitPosition(message.NextPos);
                            // allow joining heading distribution
                            SendIdle();
                            _reader.SendEof();
                        }
                        else
                        {
                            foreach (var @event in message.Events)
                            {
                                var byStream = @event.Link != null
                                               && _streamToEventType.ContainsKey(@event.Link.EventStreamId);
                                var byEvent = @event.Link == null && _eventTypes.Contains(@event.Event.EventType);
                                if (byStream)
                                { // ignore data just update positions
                                    _reader.UpdateNextStreamPosition(
                                        @event.Link.EventStreamId, @event.Link.EventNumber + 1);
                                    DeliverEventRetrievedFromTf(
                                        @event.Link, 100.0f*@event.Link.LogPosition/message.TfLastCommitPosition,
                                        @event.OriginalPosition.Value);
                                }
                                else if (byEvent)
                                {
                                    DeliverEventRetrievedFromTf(
                                        @event.Event, 100.0f*@event.Event.LogPosition/message.TfLastCommitPosition,
                                        @event.OriginalPosition.Value);
                                }
                                if (_reader.CheckEnough())
                                    return;
                            }
                        }
                        if (_disposed)
                            return;

                        break;
                    default:
                        throw new NotSupportedException(
                            String.Format("ReadEvents result code was not recognized. Code: {0}", message.Result));
                }
            }

            private void RequestTfEvents(bool delay)
            {
                if (_disposed)
                    throw new InvalidOperationException("Disposed");
                if (_reader.PauseRequested || _reader.Paused)
                    throw new InvalidOperationException("Paused or pause requested");
                if (_tfEventsRequested)
                    return;

                _tfEventsRequested = true;
                //TODO: we do not need resolve links, but lets check first with
                var readRequest = new ClientMessage.ReadAllEventsForward(
                    Guid.NewGuid(), _reader.EventReaderCorrelationId, new SendToThisEnvelope(this),
                    _fromTfPosition.CommitPosition,
                    _fromTfPosition.PreparePosition == -1 ? 0 : _fromTfPosition.PreparePosition, 111,
                    true, false, null, _readAs);
                _reader.PublishIORequest(delay, readRequest);
            }

            private void DeliverLastCommitPosition(TFPos lastPosition)
            {
                if (_reader._stopOnEof || _reader._stopAfterNEvents != null)
                    return;
                _publisher.Publish(
                    new ReaderSubscriptionMessage.CommittedEventDistributed(
                        _reader.EventReaderCorrelationId, null, lastPosition.PreparePosition, 100.0f, source: this.GetType()));
                //TODO: check was is passed here
            }

            private void DeliverEventRetrievedFromTf(EventRecord @event, float progress, TFPos position)
            {
                var resolvedEvent = new ResolvedEvent(
                    @event.EventStreamId, @event.EventNumber, @event.EventStreamId, @event.EventNumber, false, position,
                    position, @event.EventId, @event.EventType, (@event.Flags & PrepareFlags.IsJson) != 0, @event.Data,
                    @event.Metadata, null, @event.TimeStamp);

                DeliverEvent(progress, resolvedEvent, position);
            }

            private void SendIdle()
            {
                _publisher.Publish(
                    new ReaderSubscriptionMessage.EventReaderIdle(_reader.EventReaderCorrelationId, _timeProvider.Now));
            }

            public override void Dispose()
            {
                _disposed = true;
            }

            public override void RequestEvents(bool delay)
            {
                RequestTfEvents(delay: delay);
            }

            public override bool AreEventsRequested()
            {
                return _tfEventsRequested;
            }
        }

        private void DoSwitch(TFPos lastKnownIndexCheckpointPosition)
        {
            if (Paused || PauseRequested || _disposed)
                throw new InvalidOperationException("_paused || _pauseRequested || _disposed");

            // skip reading TF up to last know index checkpoint position 
            // as we could only gethere if there is no more indexed events before this point
            if (lastKnownIndexCheckpointPosition > _lastEventPosition)
                _lastEventPosition = lastKnownIndexCheckpointPosition;

            _state = new TfBased(_timeProvider, this, _lastEventPosition, this._publisher, ReadAs);
            _state.RequestEvents(delay: false);
        }
    }
}
