using System;
using System.Text;
using EventStore.Core.Data;
using EventStore.Core.TransactionLog.Data;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;
using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Tests.Services.core_projection {
	[TestFixture]
	public class when_starting_an_existing_projection_and_an_event_is_received : TestFixtureWithCoreProjectionStarted {
		private string _testProjectionState = @"{""test"":1}";

		protected override void Given() {
			ExistingEvent(
				"$projections-projection-result", "Result", @"{""c"": 100, ""p"": 50}",
				_testProjectionState);
			ExistingEvent(
				"$projections-projection-checkpoint", ProjectionEventTypes.ProjectionCheckpoint,
				@"{""c"": 100, ""p"": 50}", _testProjectionState);
			ExistingEvent(
				"$projections-projection-result", "Result", @"{""c"": 200, ""p"": 150}",
				_testProjectionState);
			ExistingEvent(
				"$projections-projection-result", "Result", @"{""c"": 300, ""p"": 250}",
				_testProjectionState);
			NoStream("$projections-projection-order");
			AllWritesToSucceed("$projections-projection-order");
		}

		protected override void When() {
			var eventId = Guid.NewGuid();
			_bus.Publish(
				EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
					new ResolvedEvent(
						"/event_category/1", -1, "/event_category/1", -1, false, new TFPos(120, 110), eventId,
						"append", false, "data", "metadata"), _subscriptionId, 0));
		}


		[Test]
		public void should_load_projection_state_handler() {
			Assert.AreEqual(1, _stateHandler._loadCalled);
			Assert.AreEqual(_testProjectionState + "data", _stateHandler._loadedState);
		}
	}
}
