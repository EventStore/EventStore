using System;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_reordering_projection_subscription
{
    [TestFixture]
    public class when_handling_an_event_after_the_delay_and_reordering_is_required : TestFixtureWithEventReorderingProjectionSubscription
    {
        private Guid _firstEventId;
        private DateTime _firstEventTimestamp;
        private Guid _secondEventId;

        protected override void When()
        {
            _firstEventId = Guid.NewGuid();
            _secondEventId = Guid.NewGuid();
            _firstEventTimestamp = DateTime.UtcNow;

            _subscription.Handle(
                new ProjectionCoreServiceMessage.CommittedEventDistributed(
                    Guid.NewGuid(), new EventPosition(200, 150), "a", 1, false,
                    ResolvedEvent.Sample(
                        _secondEventId, "bad-event-type", false, new byte[0], new byte[0], _firstEventTimestamp)));
            _subscription.Handle(
                new ProjectionCoreServiceMessage.CommittedEventDistributed(
                    Guid.NewGuid(), new EventPosition(300, 100), "b", 1, false,
                    ResolvedEvent.Sample(
                        _firstEventId, "bad-event-type", false, new byte[0], new byte[0],
                        _firstEventTimestamp.AddMilliseconds(1))));
            _subscription.Handle(
                new ProjectionCoreServiceMessage.CommittedEventDistributed(
                    Guid.NewGuid(), new EventPosition(310, 305), "a", 2, false,
                    ResolvedEvent.Sample(
                        Guid.NewGuid(), "bad-event-type", false, new byte[0], new byte[0],
                        _firstEventTimestamp.AddMilliseconds(_timeBetweenEvents))));
        }

        [Test]
        public void first_two_events_are_reordered()
        {
            Assert.AreEqual(2, _eventHandler.HandledMessages.Count);
            var first = _eventHandler.HandledMessages[0];
            var second = _eventHandler.HandledMessages[1];
            Assert.AreEqual(_firstEventId, first.Data.EventId);
            Assert.AreEqual(_secondEventId, second.Data.EventId);
        }
    }
}