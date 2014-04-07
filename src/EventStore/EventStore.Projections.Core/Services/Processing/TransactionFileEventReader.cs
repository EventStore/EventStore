using System;
using System.Diagnostics;
using System.Security.Principal;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.AwakeReaderService;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Standard;

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
        private long _lastPosition;
        private bool _eof;

        public TransactionFileEventReader(
            IODispatcher ioDispatcher, IPublisher publisher, Guid eventReaderCorrelationId, IPrincipal readAs,
            TFPos @from, ITimeProvider timeProvider, bool stopOnEof = false, bool deliverEndOfTFPosition = true,
            bool resolveLinkTos = true, int? stopAfterNEvents = null)
            : base(ioDispatcher, publisher, eventReaderCorrelationId, readAs, stopOnEof, stopAfterNEvents)
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
            _lastPosition = message.TfLastCommitPosition;
            if (message.Result == ReadAllResult.AccessDenied)
            {
                SendNotAuthorized();
                return;
            }

            var eof = message.Events.Length == 0;
            _eof = eof;
            var willDispose = _stopOnEof && eof;
            var oldFrom = _from;
            _from = message.NextPos;

            if (!willDispose)
            {
                PauseOrContinueProcessing();
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
                    DeliverEvent(@event, message.TfLastCommitPosition, oldFrom);
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

        protected override void RequestEvents()
        {
            if (_disposed) throw new InvalidOperationException("Disposed");
            if (_eventsRequested)
                throw new InvalidOperationException("Read operation is already in progress");
            if (PauseRequested || Paused)
                throw new InvalidOperationException("Paused or pause requested");
            _eventsRequested = true;


            var readEventsForward = CreateReadEventsMessage();
            if (_eof)
                _publisher.Publish(
                    new AwakeServiceMessage.SubscribeAwake(
                        new PublishEnvelope(_publisher, crossThread: true), Guid.NewGuid(), null,
                        new TFPos(_lastPosition, _lastPosition), readEventsForward));
            else
                _publisher.Publish(readEventsForward);
        }

        private Message CreateReadEventsMessage()
        {
            return new ClientMessage.ReadAllEventsForward(
                Guid.NewGuid(), EventReaderCorrelationId, new SendToThisEnvelope(this), _from.CommitPosition,
                _from.PreparePosition == -1 ? _from.CommitPosition : _from.PreparePosition, _maxReadCount, 
                _resolveLinkTos, false, null, ReadAs);
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
            EventRecord linkEvent = @event.Link;
            EventRecord targetEvent = @event.Event ?? linkEvent;
            EventRecord positionEvent = (linkEvent ?? targetEvent);

            TFPos receivedPosition = @event.OriginalPosition.Value;
            if (currentFrom > receivedPosition)
                throw new Exception(
                    string.Format(
                        "ReadFromTF returned events in incorrect order.  Last known position is: {0}.  Received position is: {1}",
                        currentFrom, receivedPosition));

            var resolvedEvent = new ResolvedEvent(@event, null);

            string deletedPartitionStreamId;
            if (resolvedEvent.IsLinkToDeletedStream && !resolvedEvent.IsLinkToDeletedStreamTombstone)
                return;

            bool isDeletedStreamEvent = StreamDeletedHelper.IsStreamDeletedEvent(
                resolvedEvent, out deletedPartitionStreamId);

            if (isDeletedStreamEvent)
                Trace.WriteLine("******");
            _publisher.Publish(
                new ReaderSubscriptionMessage.CommittedEventDistributed(
                    EventReaderCorrelationId,
                    resolvedEvent,
                    _stopOnEof ? (long?) null : receivedPosition.PreparePosition,
                    100.0f*positionEvent.LogPosition/lastCommitPosition,
                    source: this.GetType()));
            if (isDeletedStreamEvent)
                _publisher.Publish(
                    new ReaderSubscriptionMessage.EventReaderPartitionDeleted(
                        EventReaderCorrelationId, deletedPartitionStreamId, source: this.GetType(), lastEventNumber: -1,
                        deleteEventOrLinkTargetPosition: resolvedEvent.EventOrLinkTargetPosition,
                        deleteLinkOrEventPosition: resolvedEvent.LinkOrEventPosition,
                        positionStreamId: positionEvent.EventStreamId, positionEventNumber: positionEvent.EventNumber));
        }
    }
}
