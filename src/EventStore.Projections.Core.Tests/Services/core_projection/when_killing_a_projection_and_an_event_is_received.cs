using System;
using System.Linq;
using System.Text;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;
using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Tests.Services.core_projection {
	[TestFixture]
	public class when_killing_a_projection_and_an_event_is_received : TestFixtureWithCoreProjectionStarted {
		private Guid _lastEventIdBeforeKill;

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
			_lastEventIdBeforeKill = Guid.NewGuid();
			_bus.Publish(
				EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
					new ResolvedEvent(
						"/event_category/1", -1, "/event_category/1", -1, false, new TFPos(140, 130),
						_lastEventIdBeforeKill,
						"handle_this_type", false, "data2", "metadata"), _subscriptionId, 1));

			//kill the projection
			_coreProjection.Kill();

			//receive third event
			_bus.Publish(
				EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
					new ResolvedEvent(
						"/event_category/1", -1, "/event_category/1", -1, false, new TFPos(160, 150), Guid.NewGuid(),
						"handle_this_type", false, "data3", "metadata"), _subscriptionId, 2));
		}

		[Test]
		public void event_received_after_kill_is_not_processed() {
			Assert.AreEqual(2, _stateHandler._eventsProcessed);
			Assert.AreEqual(_lastEventIdBeforeKill, _stateHandler._lastProcessedEventId);
			Assert.AreEqual("data2", _stateHandler._lastProcessedData);
		}
	}
}
