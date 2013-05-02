using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Tests.Services.TimeService;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Projections.Core.EventReaders.Feeds;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.feed_reader
{
    public abstract class TestFixtureWithFeedReaderService : TestFixtureWithExistingEvents
    {
        protected FakeTimeProvider _timeProvider;

        private EventReaderCoreService _readerService;
        private FeedReaderService _feedReaderService;

        protected override void Given1()
        {
            base.Given1();
        }

        [SetUp]
        public void Setup()
        {
            _timeProvider = new FakeTimeProvider();
            _bus.Subscribe(_consumer);

            ICheckpoint writerCheckpoint = new InMemoryCheckpoint(1000);
            _readerService = new EventReaderCoreService(_bus, 10, writerCheckpoint);
            _subscriptionDispatcher =
                new PublishSubscribeDispatcher
                    <ReaderSubscriptionManagement.Subscribe,
                        ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessage
                        >(_bus, v => v.SubscriptionId, v => v.SubscriptionId);

            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CheckpointSuggested>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CommittedEventReceived>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.EofReached>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ProgressChanged>());

            _bus.Subscribe(_feedReaderService);

            _bus.Subscribe<ReaderCoreServiceMessage.StartReader>(_readerService);
            _bus.Subscribe<ReaderCoreServiceMessage.StopReader>(_readerService);
            _bus.Subscribe<ReaderCoreServiceMessage.ReaderTick>(_readerService);
            _bus.Subscribe<ReaderCoreServiceMessage.ReaderTick>(_readerService);
            _bus.Subscribe<ReaderSubscriptionMessage.CommittedEventDistributed>(_readerService);
            _bus.Subscribe<ReaderSubscriptionMessage.EventReaderEof>(_readerService);
            _bus.Subscribe<ReaderSubscriptionMessage.EventReaderIdle>(_readerService);
            _bus.Subscribe<ReaderSubscriptionManagement.Pause>(_readerService);
            _bus.Subscribe<ReaderSubscriptionManagement.Resume>(_readerService);
            _bus.Subscribe<ReaderSubscriptionManagement.Subscribe>(_readerService);
            _bus.Subscribe<ReaderSubscriptionManagement.Unsubscribe>(_readerService);
            
            Given();
            When();
        }

        protected abstract void When();
    }
}