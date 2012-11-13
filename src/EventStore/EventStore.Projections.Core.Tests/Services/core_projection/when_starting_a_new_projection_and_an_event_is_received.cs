using System;
using System.Linq;
using System.Text;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    [TestFixture]
    public class when_starting_a_new_projection_and_an_event_is_received : TestFixtureWithCoreProjectionStarted
    {
        protected override void Given()
        {
            NoStream("$projections-projection-state");
            NoStream("$projections-projection-checkpoint");
        }

        protected override void When()
        {
            var eventId = Guid.NewGuid();
            _coreProjection.Handle(
                ProjectionSubscriptionMessage.CommittedEventReceived.Sample(Guid.Empty, new EventPosition(120, 110), "/event_category/1", -1, false,
                       new Event(
                           eventId, "handle_this_type", false, Encoding.UTF8.GetBytes("data"),
                           Encoding.UTF8.GetBytes("metadata")), 0));
        }

        [Test]
        public void should_initialize_projection_state_handler()
        {
            Assert.AreEqual(1, _stateHandler._initializeCalled);
        }

    }
}