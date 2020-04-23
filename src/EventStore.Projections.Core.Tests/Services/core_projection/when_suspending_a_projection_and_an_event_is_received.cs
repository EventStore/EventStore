using System;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.core_projection {
	[TestFixture]
	public class when_suspending_a_projection_and_an_event_is_received : TestFixtureWithCoreProjectionStarted {
		private Guid _lastEventIdBeforeSuspension;

		protected override void Given() {
			AllWritesSucceed();
			NoOtherStreams();
		}

		protected override void When() {
			//receive first event
			_bus.Publish(
				EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
					new ResolvedEvent(
						"/event_category/1", -1, "/event_category/1", -1, false, new TFPos(120, 110), Guid.NewGuid(),
						"handle_this_type", false, "data1", "metadata"), _subscriptionId, 0));

			//receive second event
			_lastEventIdBeforeSuspension = Guid.NewGuid();
			_bus.Publish(
				EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
					new ResolvedEvent(
						"/event_category/1", -1, "/event_category/1", -1, false, new TFPos(140, 130),
						_lastEventIdBeforeSuspension,
						"handle_this_type", false, "data2", "metadata"), _subscriptionId, 1));

			//suspend the projection
			var suspended = _coreProjection.Suspend();
			Assert.True(suspended, "Projection was not suspended");

			//receive third event
			_bus.Publish(
				EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
					new ResolvedEvent(
						"/event_category/1", -1, "/event_category/1", -1, false, new TFPos(160, 150), Guid.NewGuid(),
						"handle_this_type", false, "data3", "metadata"), _subscriptionId, 2));
		}

		[Test]
		public void event_received_after_suspend_is_not_processed() {
			Assert.AreEqual(2, _stateHandler._eventsProcessed);
			Assert.AreEqual(_lastEventIdBeforeSuspension, _stateHandler._lastProcessedEventId);
			Assert.AreEqual("data2", _stateHandler._lastProcessedData);
		}
	}
}
