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
    public class TransactionFileEventReader : EventReader, IHandle<ClientMessage.ReadAllEventsForwardCompleted>
    {
        private bool _eventsRequested;
        private int _maxReadCount = 250;
        private TFPos _from;
        private readonly bool _deliverEndOfTfPosition;
        private readonly bool _resolveLinkTos;
        private readonly ITimeProvider _timeProvider;
        private int _deliveredEvents;

        public TransactionFileEventReader(
            IPublisher publisher, Guid eventReaderCorrelationId, IPrincipal readAs, TFPos @from,
            ITimeProvider timeProvider, bool stopOnEof = false, bool deliverEndOfTFPosition = true,
            bool resolveLinkTos = true, int? stopAfterNEvents = null)
            : base(publisher, eventReaderCorrelationId, readAs, stopOnEof, stopAfterNEvents)
        {
            if (publisher == null) throw new ArgumentNullException("publisher");
            _from = @from;
            _deliverEndOfTfPosition = deliverEndOfTFPosition;
            _resolveLinkTos = resolveLinkTos;
            _timeProvider = timeProvider;
        }

        protected override bool AreEventsRequested()
        {
            return _eventsRequested;
        }

        public void Handle(ClientMessage.ReadAllEventsForwardCompleted message)
        {
            if (_disposed)
                return;
            if (!_eventsRequested)
                throw new InvalidOperationException("Read events has not been requested");
            if (Paused)
                throw new InvalidOperationException("Paused");
            _eventsRequested = false;

            if (message.Result == ReadAllResult.AccessDenied)
            {
                SendNotAuthorized();
                return;
            }

            var eof = message.Events.Length == 0;
            var willDispose = _stopOnEof && eof;
            var oldFrom = _from;
            _from = message.NextPos;

            if (!willDispose)
            {
                PauseOrContinueProcessing(delay: eof);
            }

            if (eof)
            {
                // the end
                if (_deliverEndOfTfPosition)
                    DeliverLastCommitPosition(_from);
                // allow joining heading distribution
                SendIdle();
                SendEof();
            }
            else
            {
                for (int index = 0; index < message.Events.Length; index++)
                {
                    var @event = message.Events[index];
                    DeliverEvent(@event, message.TfEofPosition, oldFrom);
                    if (CheckEnough())
                        return;
                }
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

        private void SendIdle()
        {
            _publisher.Publish(
                new ReaderSubscriptionMessage.EventReaderIdle(EventReaderCorrelationId, _timeProvider.Now));
        }

        protected override void RequestEvents(bool delay)
        {
            if (_disposed) throw new InvalidOperationException("Disposed");
            if (_eventsRequested)
                throw new InvalidOperationException("Read operation is already in progress");
            if (PauseRequested || Paused)
                throw new InvalidOperationException("Paused or pause requested");
            _eventsRequested = true;


            var readEventsForward = CreateReadEventsMessage();
            if (delay)
                _publisher.Publish(
                    TimerMessage.Schedule.Create(
                        TimeSpan.FromMilliseconds(250), new PublishEnvelope(_publisher, crossThread: true),
                        readEventsForward));
            else
                _publisher.Publish(readEventsForward);
        }

        private Message CreateReadEventsMessage()
        {
            return new ClientMessage.ReadAllEventsForward(
                EventReaderCorrelationId, new SendToThisEnvelope(this), _from.CommitPosition,
                _from.PreparePosition == -1 ? _from.CommitPosition : _from.PreparePosition, _maxReadCount, 
                _resolveLinkTos, null, ReadAs);
        }

        private void DeliverLastCommitPosition(TFPos lastPosition)
        {
            if (_stopOnEof || _stopAfterNEvents != null)
                return;
            _publisher.Publish(
                new ReaderSubscriptionMessage.CommittedEventDistributed(
                    EventReaderCorrelationId, null, lastPosition.PreparePosition, 100.0f, source: this.GetType()));
                //TODO: check was is passed here
        }

        private void DeliverEvent(
            EventStore.Core.Data.ResolvedEvent @event, long lastCommitPosition, TFPos currentFrom)
        {
            _deliveredEvents++;
            EventRecord positionEvent = (@event.Link ?? @event.Event);
            TFPos receivedPosition = @event.OriginalPosition.Value;
            if (currentFrom > receivedPosition)
                throw new Exception(
                    string.Format(
                        "ReadFromTF returned events in incorrect order.  Last known position is: {0}.  Received position is: {1}",
                        currentFrom, receivedPosition));
            TFPos originalPosition;
            if (@event.IsResolved)
            {
                if (positionEvent.Metadata != null && positionEvent.Metadata.Length > 0)
                {
                    var parsedPosition =
                        positionEvent.Metadata.ParseCheckpointTagJson().Position;
                    originalPosition = parsedPosition != new TFPos(long.MinValue, long.MinValue)
                                           ? parsedPosition
                                           : new TFPos(-1, @event.OriginalEvent.LogPosition);
                }
                else
                    originalPosition = new TFPos(-1, @event.OriginalEvent.LogPosition);
            }
            else
            {
                originalPosition = receivedPosition;
            }

            _publisher.Publish(
                new ReaderSubscriptionMessage.CommittedEventDistributed(
                    EventReaderCorrelationId,
                    new ResolvedEvent(
                        positionEvent.EventStreamId, positionEvent.EventNumber, @event.Event.EventStreamId,
                        @event.Event.EventNumber, @event.Link != null, receivedPosition,
                        originalPosition, @event.Event.EventId, @event.Event.EventType,
                        (@event.Event.Flags & PrepareFlags.IsJson) != 0, @event.Event.Data, @event.Event.Metadata,
                        @event.Link == null ? null : @event.Link.Metadata, positionEvent.TimeStamp),
                    _stopOnEof ? (long?) null : receivedPosition.PreparePosition,
                    100.0f*positionEvent.LogPosition/lastCommitPosition, source: this.GetType()));
        }
    }
}
