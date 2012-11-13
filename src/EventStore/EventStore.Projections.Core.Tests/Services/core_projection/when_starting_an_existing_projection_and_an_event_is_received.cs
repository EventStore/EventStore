using System;
using System.Linq;
using System.Text;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    [TestFixture]
    public class when_starting_an_existing_projection_and_an_event_is_received : TestFixtureWithCoreProjectionStarted
    {
        private string _testProjectionState = @"{""test"":1}";

        protected override void Given()
        {
            ExistingEvent(
                "$projections-projection-state", "StateUpdated",
                @"{""CommitPosition"": 100, ""PreparePosition"": 50, ""LastSeenEvent"": """
                + Guid.NewGuid().ToString("D") + @"""}", _testProjectionState);
            ExistingEvent(
                "$projections-projection-checkpoint", "ProjectionCheckpoint",
                @"{""CommitPosition"": 100, ""PreparePosition"": 50, ""LastSeenEvent"": """
                + Guid.NewGuid().ToString("D") + @"""}", _testProjectionState);
            ExistingEvent(
                "$projections-projection-state", "StateUpdated",
                @"{""CommitPosition"": 200, ""PreparePosition"": 150, ""LastSeenEvent"": """
                + Guid.NewGuid().ToString("D") + @"""}", _testProjectionState);
            ExistingEvent(
                "$projections-projection-state", "StateUpdated",
                @"{""CommitPosition"": 300, ""PreparePosition"": 250, ""LastSeenEvent"": """
                + Guid.NewGuid().ToString("D") + @"""}", _testProjectionState);
        }

        protected override void When()
        {
            var eventId = Guid.NewGuid();
            _coreProjection.Handle(
                ProjectionSubscriptionMessage.CommittedEventReceived.Sample(Guid.Empty, new EventPosition(120, 110), "/event_category/1", -1, false,
                       new Event(
                           eventId, "append", false, Encoding.UTF8.GetBytes("data"),
                           Encoding.UTF8.GetBytes("metadata")), 0));
        }


        [Test]
        public void should_load_projection_state_handler()
        {
            Assert.AreEqual(1, _stateHandler._loadCalled);
            Assert.AreEqual(_testProjectionState + "data", _stateHandler._loadedState);
        }

    }
}