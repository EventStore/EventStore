using System;
using System.Text;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.core_projection {
	[TestFixture]
	public class when_starting_a_new_projection_and_an_event_is_received : TestFixtureWithCoreProjectionStarted {
		protected override void Given() {
			NoStream("$projections-projection-result");
			NoStream("$projections-projection-order");
			AllWritesToSucceed("$projections-projection-order");
			NoStream("$projections-projection-checkpoint");
		}

		protected override void When() {
			var eventId = Guid.NewGuid();
			_bus.Publish(
				EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
					new ResolvedEvent(
						"/event_category/1", -1, "/event_category/1", -1, false, new TFPos(120, 110), eventId,
						"handle_this_type", false, "data", "metadata"), _subscriptionId, 0));
		}

		[Test]
		public void should_initialize_projection_state_handler() {
			Assert.AreEqual(1, _stateHandler._initializeCalled);
		}
	}
}
