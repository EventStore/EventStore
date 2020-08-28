using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.TransactionLog.Data;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.projections_manager.managed_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_reader.heading_event_reader {
	[TestFixture]
	public class when_the_heading_event_reader_with_a_subscribed_projection_handles_an_idle_notification :
		TestFixtureWithReadWriteDispatchers {
		private HeadingEventReader _point;

		//private Exception _exception;
		private Guid _distibutionPointCorrelationId;
		private FakeReaderSubscription _subscription;
		private Guid _projectionSubscriptionId;

		[SetUp]
		public void setup() {
			//_exception = null;
			try {
				_point = new HeadingEventReader(10, _bus);
			} catch (Exception) {
				//_exception = ex;
			}

			_distibutionPointCorrelationId = Guid.NewGuid();
			_point.Start(
				_distibutionPointCorrelationId,
				new TransactionFileEventReader(_bus, _distibutionPointCorrelationId, null, new TFPos(0, -1),
					new RealTimeProvider()));
			DateTime timestamp = DateTime.UtcNow;
			_point.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					_distibutionPointCorrelationId, new TFPos(20, 10), "stream", 10, false, Guid.NewGuid(),
					"type", false, new byte[0], new byte[0], timestamp));
			_point.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					_distibutionPointCorrelationId, new TFPos(40, 30), "stream", 11, false, Guid.NewGuid(),
					"type", false, new byte[0], new byte[0], timestamp.AddMilliseconds(1)));
			_subscription = new FakeReaderSubscription();
			_projectionSubscriptionId = Guid.NewGuid();
			_point.TrySubscribe(_projectionSubscriptionId, _subscription, 30);
			_point.Handle(
				new ReaderSubscriptionMessage.EventReaderIdle(
					_distibutionPointCorrelationId, timestamp.AddMilliseconds(1100)));
		}


		[Test]
		public void projection_receives_events_after_the_subscription_point() {
			Assert.AreEqual(1, _subscription.ReceivedIdleNotifications.Count());
		}
	}
}
