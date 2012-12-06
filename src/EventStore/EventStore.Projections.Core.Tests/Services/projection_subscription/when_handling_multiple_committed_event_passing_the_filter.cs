using System;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_subscription
{
    [TestFixture]
    public class when_handling_multiple_committed_event_passing_the_filter : TestFixtureWithProjectionSubscription
    {
        protected override void When()
        {
            _subscription.Handle(
                new ProjectionCoreServiceMessage.CommittedEventDistributed(
                    Guid.NewGuid(), new EventPosition(200, 150), "test-stream", 1, false,
                    ResolvedEvent.Sample(Guid.NewGuid(), "bad-event-type", false, new byte[0], new byte[0])));
            _subscription.Handle(
                new ProjectionCoreServiceMessage.CommittedEventDistributed(
                    Guid.NewGuid(), new EventPosition(300, 250), "test-stream", 2, false,
                    ResolvedEvent.Sample(Guid.NewGuid(), "bad-event-type", false, new byte[0], new byte[0])));
        }

        [Test]
        public void events_passed_to_downstream_handler_have_correct_subscription_sequence_numbers()
        {
            Assert.AreEqual(2, _eventHandler.HandledMessages.Count);

            Assert.AreEqual(0, _eventHandler.HandledMessages[0].SubscriptionMessageSequenceNumber);
            Assert.AreEqual(1, _eventHandler.HandledMessages[1].SubscriptionMessageSequenceNumber);
        }
    }
}