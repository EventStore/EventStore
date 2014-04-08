using System;
using System.Collections.Generic;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Tests.Services.event_reader.heading_event_reader
{
    class FakeReaderSubscription : IReaderSubscription
    {
        private readonly List<ReaderSubscriptionMessage.CommittedEventDistributed> _receivedEvents =
            new List<ReaderSubscriptionMessage.CommittedEventDistributed>();

        private readonly List<ReaderSubscriptionMessage.EventReaderIdle> _receivedIdleNotifications =
            new List<ReaderSubscriptionMessage.EventReaderIdle>();

        private readonly List<ReaderSubscriptionMessage.EventReaderStarting> _receivedStartingNotifications =
            new List<ReaderSubscriptionMessage.EventReaderStarting>();

        private readonly List<ReaderSubscriptionMessage.EventReaderEof> _receivedEofNotifications =
            new List<ReaderSubscriptionMessage.EventReaderEof>();

        private readonly List<ReaderSubscriptionMessage.EventReaderPartitionEof> _receivedPartitionEofNotifications =
            new List<ReaderSubscriptionMessage.EventReaderPartitionEof>();

        private readonly List<ReaderSubscriptionMessage.EventReaderPartitionDeleted> _receivedPartitionDeletedNotifications =
            new List<ReaderSubscriptionMessage.EventReaderPartitionDeleted>();

        private readonly List<ReaderSubscriptionMessage.EventReaderPartitionMeasured> _receivedPartitionMeasuredNotifications =
            new List<ReaderSubscriptionMessage.EventReaderPartitionMeasured>();

        private readonly List<ReaderSubscriptionMessage.EventReaderNotAuthorized> _receivedNotAuthorizedNotifications =
            new List<ReaderSubscriptionMessage.EventReaderNotAuthorized>();

        public void Handle(ReaderSubscriptionMessage.CommittedEventDistributed message)
        {
            _receivedEvents.Add(message);
        }

        public List<ReaderSubscriptionMessage.CommittedEventDistributed> ReceivedEvents
        {
            get { return _receivedEvents; }
        }

        public List<ReaderSubscriptionMessage.EventReaderIdle> ReceivedIdleNotifications
        {
            get { return _receivedIdleNotifications; }
        }

        public List<ReaderSubscriptionMessage.EventReaderStarting> ReceivedStartingNotifications
        {
            get { return _receivedStartingNotifications; }
        }

        public List<ReaderSubscriptionMessage.EventReaderEof> ReceivedEofNotifications
        {
            get { return _receivedEofNotifications; }
        }

        public List<ReaderSubscriptionMessage.EventReaderPartitionEof> ReceivedPartitionEofNotifications
        {
            get { return _receivedPartitionEofNotifications; }
        }

        public List<ReaderSubscriptionMessage.EventReaderPartitionDeleted> ReceivedPartitionDeletedNotifications
        {
            get { return _receivedPartitionDeletedNotifications; }
        }

        public List<ReaderSubscriptionMessage.EventReaderNotAuthorized> ReceivedNotAuthorizedNotifications
        {
            get { return _receivedNotAuthorizedNotifications; }
        }

        public void Handle(ReaderSubscriptionMessage.EventReaderIdle message)
        {
            _receivedIdleNotifications.Add(message);
        }

        public void Handle(ReaderSubscriptionMessage.EventReaderStarting message)
        {
            _receivedStartingNotifications.Add(message);
        }

        public void Handle(ReaderSubscriptionMessage.EventReaderEof message)
        {
            _receivedEofNotifications.Add(message);
        }

        public void Handle(ReaderSubscriptionMessage.EventReaderPartitionEof message)
        {
            _receivedPartitionEofNotifications.Add(message);
        }

        public void Handle(ReaderSubscriptionMessage.EventReaderPartitionDeleted message)
        {
            _receivedPartitionDeletedNotifications.Add(message);
        }

        public void Handle(ReaderSubscriptionMessage.EventReaderPartitionMeasured message)
        {
            _receivedPartitionMeasuredNotifications.Add(message);
        }

        public void Handle(ReaderSubscriptionMessage.EventReaderNotAuthorized message)
        {
            _receivedNotAuthorizedNotifications.Add(message);
        }

        public string Tag
        {
            get { return "FakeReaderSubscription"; }
        }

        public IEventReader CreatePausedEventReader(
            IPublisher publisher, IODispatcher ioDispatcher, Guid forkedEventReaderId)
        {
            throw new NotImplementedException();
        }
    }
}
