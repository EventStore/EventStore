using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.event_reader.heading_event_reader;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_reader.event_reader_core_service {
	[TestFixture]
	public class when_starting_reader_from_end_without_checkpoint : TestFixtureWithEventReaderService {
		private FakeReaderSubscription _subscription;

		protected override bool GivenHeadingReaderRunning() {
			return true;
		}
		
		[SetUp]
		public new void When() {
			var headingEventReaderId = GetReaderId();
			
			_readerService.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					headingEventReaderId, new TFPos(20, 10), "stream", 10, false, Guid.NewGuid(),
					"type", false, new byte[0], new byte[0]));
			_readerService.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					headingEventReaderId, new TFPos(40, 30), "stream", 11, false, Guid.NewGuid(),
					"type", false, new byte[0], new byte[0]));
			
			_subscription = new FakeReaderSubscription();
			var readerStrategy = new FakeReaderStrategy(_subscription);
			var zeroTag = readerStrategy.PositionTagger.MakeZeroCheckpointTag();
			var readerSubscriptionOptions = new ReaderSubscriptionOptions
				(10000, 4000, 10000, false, null, subscribeFromEnd: true);
			
			var subscribeMessage = new ReaderSubscriptionManagement.Subscribe
				(Guid.NewGuid(), zeroTag, readerStrategy, readerSubscriptionOptions);
			
			_readerService.Handle(subscribeMessage);
			
			_readerService.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					headingEventReaderId, new TFPos(60, 50), "stream", 12, false, Guid.NewGuid(),
					"type", false, new byte[0], new byte[0]));
		}

		[Test]
		public void it_does_not_get_events_before_subscribing() {
			Assert.AreEqual(false, _subscription.ReceivedEvents
				.Any(v => v.Data.Position.PreparePosition <= 30));
		}

		[Test]
		public void it_gets_events_after_subscribing() {
			Assert.AreEqual(true, _subscription.ReceivedEvents
				.Any(v => v.Data.Position.PreparePosition == 50));
		}
	}
}
