using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Helpers;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_reader.heading_event_reader {
	[TestFixture]
	public class when_the_heading_event_reader_subscribes_a_projection_to_the_end : TestFixtureWithReadWriteDispatchers {
		private HeadingEventReader _point;
		private Exception _exception;
		private Guid _distributionPointCorrelationId;
		private FakeReaderSubscription _subscription;
		private Guid _projectionSubscriptionId;

		[SetUp]
		public void setup() {
			_exception = null;
			try {
				_point = new HeadingEventReader(10, _bus);
			} catch (Exception ex) {
				_exception = ex;
			}

			Assume.That(_exception == null);

			_distributionPointCorrelationId = Guid.NewGuid();
			_point.Start(
				_distributionPointCorrelationId,
				new TransactionFileEventReader(_bus, _distributionPointCorrelationId, null, new TFPos(0, -1),
					new RealTimeProvider()));
			_point.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					_distributionPointCorrelationId, new TFPos(20, 10), "stream", 10, false, Guid.NewGuid(),
					"type", false, new byte[0], new byte[0]));
			_point.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					_distributionPointCorrelationId, new TFPos(40, 30), "stream", 11, false, Guid.NewGuid(),
					"type", false, new byte[0], new byte[0]));
			_subscription = new FakeReaderSubscription();
			_projectionSubscriptionId = Guid.NewGuid();
			_point.TrySubscribeFromEnd(_projectionSubscriptionId, _subscription);
			_point.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					_distributionPointCorrelationId, new TFPos(60, 50), "stream", 12, false, Guid.NewGuid(),
					"type", false, new byte[0], new byte[0]));
		}


		[Test]
		public void projection_should_not_receive_cached_events() {
			Assert.AreEqual(false, _subscription.ReceivedEvents.Any(v => v.Data.Position.PreparePosition <= 30));
		}

		[Test]
		public void projection_should_receive_event_committed_after_subscribing() {
			Assert.AreEqual(true, _subscription.ReceivedEvents.Any(v => v.Data.Position.PreparePosition == 50));
		}

		[Test]
		public void it_can_be_unsubscribed() {
			_point.Unsubscribe(_projectionSubscriptionId);
		}

		[Test]
		public void no_other_projection_can_subscribe_with_the_same_projection_id() {
			Assert.Throws<InvalidOperationException>(() => {
				_point.TrySubscribe(_projectionSubscriptionId, _subscription, 30);
			});
		}
	}
}
