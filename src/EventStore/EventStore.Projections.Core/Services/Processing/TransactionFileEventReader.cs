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
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class TransactionFileEventReader : EventReader
    {
        private bool _eventsRequested = false;
        private int _maxReadCount = 50;
        private EventPosition _from;
        private readonly bool _deliverEndOfTfPosition;
        private readonly ITimeProvider _timeProvider;

        public TransactionFileEventReader(IPublisher publisher, Guid distibutionPointCorrelationId, EventPosition @from, ITimeProvider timeProvider, bool stopOnEof = false, bool deliverEndOfTFPosition = true)
            : base(publisher, distibutionPointCorrelationId, stopOnEof)
        {
            if (publisher == null) throw new ArgumentNullException("publisher");
            _from = @from;
            _deliverEndOfTfPosition = deliverEndOfTFPosition;
            _timeProvider = timeProvider;
        }

        protected override void RequestEvents()
        {
            RequestEvents(delay: false);
        }

        protected override string FromAsText()
        {
            return _from.ToString();
        }

        protected override bool AreEventsRequested()
        {
            return _eventsRequested;
        }

        public override void Handle(ClientMessage.ReadStreamEventsForwardCompleted message)
        {
        }

        public override void Handle(ClientMessage.ReadAllEventsForwardCompleted message)
        {
            if (_disposed)
                return;
            if (!_eventsRequested)
                throw new InvalidOperationException("Read events has not been requested");
            if (_paused)
                throw new InvalidOperationException("Paused");
            _eventsRequested = false;

            if (message.Result.Records.Length == 0)
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
                for (int index = 0; index < message.Result.Records.Length; index++)
                {
                    var @event = message.Result.Records[index];
                    DeliverEvent(@event, message.Result.TfEofPosition);
                }
                _from = message.Result.NextPos;
            }

            if (_disposed)
                return;

            if (_pauseRequested)
                _paused = true;
            else if (message.Result.Records.Length == 0)
                RequestEvents(delay: true);
            else
                _publisher.Publish(CreateTickMessage());

        }

        private void SendIdle()
        {
            _publisher.Publish(
                new ProjectionCoreServiceMessage.EventReaderIdle(
                    _distibutionPointCorrelationId, _timeProvider.Now));
        }

        private void RequestEvents(bool delay)
        {
            if (_disposed) throw new InvalidOperationException("Disposed");
            if (_eventsRequested)
                throw new InvalidOperationException("Read operation is already in progress");
            if (_pauseRequested || _paused)
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
                _distibutionPointCorrelationId, new SendToThisEnvelope(this), _from.CommitPosition,
                _from.PreparePosition == -1 ? _from.CommitPosition : _from.PreparePosition, _maxReadCount, true);
        }

        private void DeliverLastCommitPosition(EventPosition lastPosition)
        {
            _publisher.Publish(
                new ProjectionCoreServiceMessage.CommittedEventDistributed(
                    _distibutionPointCorrelationId, default(EventPosition), null, int.MinValue,
                    null, int.MinValue, false, null, lastPosition.PreparePosition, 100.0f)); //TODO: check was is passed here
        }

        private void DeliverEvent(ResolvedEventRecord @event, long lastCommitPosition)
        {
            EventRecord positionEvent = (@event.Link ?? @event.Event);
            var receivedPosition = new EventPosition(@event.CommitPosition, positionEvent.LogPosition);
            if (_from > receivedPosition)
                throw new Exception(
                    string.Format(
                        "ReadFromTF returned events in incorrect order.  Last known position is: {0}.  Received position is: {1}",
                        _from, receivedPosition));

            _publisher.Publish(
                new ProjectionCoreServiceMessage.CommittedEventDistributed(
                    _distibutionPointCorrelationId, receivedPosition, positionEvent.EventStreamId,
                    positionEvent.EventNumber, @event.Event.EventStreamId, @event.Event.EventNumber, @event.Link != null,
                    ResolvedEvent.Create(
                        @event.Event.EventId, @event.Event.EventType, (@event.Event.Flags & PrepareFlags.IsJson) != 0,
                        @event.Event.Data, @event.Event.Metadata, positionEvent.TimeStamp),
                    receivedPosition.PreparePosition, 100.0f*positionEvent.LogPosition/lastCommitPosition));
        }
    }
}
