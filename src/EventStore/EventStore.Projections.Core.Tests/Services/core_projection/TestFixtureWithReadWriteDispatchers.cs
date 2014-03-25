using System;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    public abstract class TestFixtureWithReadWriteDispatchers : 
        EventStore.Core.Tests.Helpers.TestFixtureWithReadWriteDispatchers
    {
        protected ReaderSubscriptionDispatcher _subscriptionDispatcher;

        protected readonly ProjectionStateHandlerFactory _handlerFactory = new ProjectionStateHandlerFactory();

        [SetUp]
        public void SetUp()
        {
            _subscriptionDispatcher = new ReaderSubscriptionDispatcher(
                _bus);
            _bus.Subscribe(
                _subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CommittedEventReceived>());
            _bus.Subscribe(
                _subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CheckpointSuggested>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.EofReached>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionEofReached>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionMeasured>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionDeleted>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ProgressChanged>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.SubscriptionStarted>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.NotAuthorized>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ReaderAssignedReader>());
        }
    }
}
