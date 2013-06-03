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
    public class StreamEventReader : EventReader, IHandle<ClientMessage.ReadStreamEventsForwardCompleted>
    {
        private readonly string _streamName;
        private int _fromSequenceNumber;
        private readonly ITimeProvider _timeProvider;
        private readonly bool _resolveLinkTos;

        private bool _eventsRequested;
        private int _maxReadCount = 111;
        private int _deliveredEvents;

        public StreamEventReader(
            IPublisher publisher, Guid eventReaderCorrelationId, IPrincipal readAs, string streamName,
            int fromSequenceNumber, ITimeProvider timeProvider, bool resolveLinkTos, bool stopOnEof = false,
            int? stopAfterNEvents = null)
            : base(publisher, eventReaderCorrelationId, readAs, stopOnEof, stopAfterNEvents)
        {
            if (fromSequenceNumber < 0) throw new ArgumentException("fromSequenceNumber");
            if (streamName == null) throw new ArgumentNullException("streamName");
            if (string.IsNullOrEmpty(streamName)) throw new ArgumentException("streamName");
            _streamName = streamName;
            _fromSequenceNumber = fromSequenceNumber;
            _timeProvider = timeProvider;
            _resolveLinkTos = resolveLinkTos;
        }

        protected override bool AreEventsRequested()
        {
            return _eventsRequested;
        }

        public void Handle(ClientMessage.ReadStreamEventsForwardCompleted message)
        {
            if (_disposed)
                return;
            if (!_eventsRequested)
                throw new InvalidOperationException("Read events has not been requested");
            if (message.EventStreamId != _streamName)
                throw new InvalidOperationException(
                    string.Format("Invalid stream name: {0}.  Expected: {1}", message.EventStreamId, _streamName));
            if (Paused)
                throw new InvalidOperationException("Paused");
            _eventsRequested = false;
            switch (message.Result)
            {
                case ReadStreamResult.NoStream:
                    DeliverSafeJoinPosition(GetLastCommitPositionFrom(message)); // allow joining heading distribution
                    PauseOrContinueProcessing(delay: true);
                    SendIdle();
                    SendEof();
                    break;
                case ReadStreamResult.Success:
                    var oldFromSequenceNumber = _fromSequenceNumber;
                    _fromSequenceNumber = message.NextEventNumber;
                    var eof = message.Events.Length == 0;
                    var willDispose = eof && _stopOnEof;

                    if (!willDispose)
                    {
                        PauseOrContinueProcessing(delay: eof);
                    }
                    if (eof)
                    {
                        // the end
                        DeliverSafeJoinPosition(GetLastCommitPositionFrom(message));
                        SendIdle();
                        SendEof();
                    }
                    else
                    {
                        for (int index = 0; index < message.Events.Length; index++)
                        {
                            var @event = message.Events[index].Event;
                            var @link = message.Events[index].Link;
                            DeliverEvent(
                                @event, @link, 100.0f*(link ?? @event).EventNumber/message.LastEventNumber,
                                ref oldFromSequenceNumber);
                            if (CheckEnough())
                                return;
                        }
                    }

                    break;
                case ReadStreamResult.AccessDenied:
                    SendNotAuthorized();
                    return;
                default:
                    throw new NotSupportedException(
                        string.Format("ReadEvents result code was not recognized. Code: {0}", message.Result));
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
            return new ClientMessage.ReadStreamEventsForward(
                EventReaderCorrelationId, new SendToThisEnvelope(this), _streamName, _fromSequenceNumber,
                _maxReadCount, _resolveLinkTos, null, ReadAs);
        }

        private void DeliverSafeJoinPosition(long? safeJoinPosition)
        {
            if (_stopOnEof || _stopAfterNEvents != null || safeJoinPosition == null || safeJoinPosition == -1)
                return; //TODO: this should not happen, but StorageReader does not return it now
            _publisher.Publish(
                new ReaderSubscriptionMessage.CommittedEventDistributed(
                    EventReaderCorrelationId, null, safeJoinPosition, 100.0f, source: this.GetType()));
        }

        private void DeliverEvent(EventRecord @event, EventRecord link, float progress, ref int sequenceNumber)
        {
            _deliveredEvents++;

            EventRecord positionEvent = (link ?? @event);
            if (positionEvent.EventNumber != sequenceNumber)
                throw new InvalidOperationException(
                    string.Format(
                        "Event number {0} was expected in the stream {1}, but event number {2} was received",
                        sequenceNumber, _streamName, positionEvent.EventNumber));
            sequenceNumber = positionEvent.EventNumber + 1;
            var resolvedLinkTo = positionEvent.EventStreamId != @event.EventStreamId
                                 || positionEvent.EventNumber != @event.EventNumber;
            _publisher.Publish(
                //TODO: publish both link and event data
                new ReaderSubscriptionMessage.CommittedEventDistributed(
                    EventReaderCorrelationId,
                    new ResolvedEvent(
                        positionEvent.EventStreamId, positionEvent.EventNumber, @event.EventStreamId, @event.EventNumber,
                        resolvedLinkTo, new TFPos(-1, positionEvent.LogPosition), new TFPos(-1, @event.LogPosition),
                        @event.EventId, @event.EventType, (@event.Flags & PrepareFlags.IsJson) != 0, @event.Data,
                        @event.Metadata, link == null ? null : link.Metadata, positionEvent.TimeStamp),
                    _stopOnEof ? (long?) null : positionEvent.LogPosition, progress, source: this.GetType()));
        }
    }
}
