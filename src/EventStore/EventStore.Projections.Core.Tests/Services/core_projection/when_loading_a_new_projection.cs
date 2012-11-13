using System.Linq;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    [TestFixture]
    public class when_loading_a_new_projection : TestFixtureWithCoreProjectionLoaded
    {
        protected override void Given()
        {
            NoStream("$projections-projection-state");
            NoStream("$projections-projection-checkpoint");
        }

        protected override void When()
        {
        }

        [Test]
        public void should_subscribe_from_beginning()
        {
            Assert.AreEqual(1, _subscribeProjectionHandler.HandledMessages.Count);
            Assert.AreEqual(0, _subscribeProjectionHandler.HandledMessages[0].FromPosition.Position.CommitPosition);
            Assert.AreEqual(-1, _subscribeProjectionHandler.HandledMessages[0].FromPosition.Position.PreparePosition);
        }

        [Test]
        public void should_subscribe_non_null_subscriber()
        {
            Assert.NotNull(_subscribeProjectionHandler.HandledMessages[0].Subscriber);
        }

        [Test]
        public void should_not_initialize_projection_state_handler()
        {
            Assert.AreEqual(0, _stateHandler._initializeCalled);
        }

        [Test]
        public void should_not_publish_started_message()
        {
            Assert.AreEqual(0, _consumer.HandledMessages.OfType<CoreProjectionManagementMessage.Started>().Count());
        }
    }
}