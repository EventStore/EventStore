using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.event_reader.heading_event_reader;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_reader.event_reader_core_service {
	[TestFixture]
	public class when_starting_reader_from_end_with_existing_checkpoint : TestFixtureWithEventReaderService {
		private FakeReaderSubscription _subscription;
		private Guid _headingEventReaderId;

		protected override bool GivenHeadingReaderRunning() {
			return true;
		}
		
		[SetUp]
		public new void When() {
			_headingEventReaderId = GetReaderId();

			var firstEvent =
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					_headingEventReaderId, new TFPos(20, 10), "stream", 10, false, Guid.NewGuid(),
					"type", false, new byte[0], new byte[0]);
			_readerService.Handle(firstEvent);
			_readerService.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					_headingEventReaderId, new TFPos(40, 30), "stream", 11, false, Guid.NewGuid(),
					"type", false, new byte[0], new byte[0]));
			_readerService.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					_headingEventReaderId, new TFPos(60, 50), "stream", 12, false, Guid.NewGuid(),
					"type", false, new byte[0], new byte[0]));
        		
			_subscription = new FakeReaderSubscription();
			var readerStrategy = new FakeReaderStrategy(_subscription);
			var zeroTag = readerStrategy.PositionTagger.MakeZeroCheckpointTag();
			var readerSubscriptionOptions = new ReaderSubscriptionOptions
				(10000, 4000, 10000, false, null, subscribeFromEnd: true);
			
			// Create a checkpoint for the first event
			var checkpoint = readerStrategy.PositionTagger.MakeCheckpointTag(zeroTag, firstEvent);

			var subscribeMessage = new ReaderSubscriptionManagement.Subscribe
				(Guid.NewGuid(), checkpoint, readerStrategy, readerSubscriptionOptions);
			
			_readerService.Handle(subscribeMessage);
			var readerId = _subscription.EventReader.EventReaderId;
			
			// Send committed event distributed to the reader with a safe join position.
			// This will switch the projection over to the heading event reader and catch up missed events
			_readerService.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					readerId, new TFPos(60, 50), new TFPos(60, 50), "stream", 12, "stream", 12, false, Guid.NewGuid(),
					"type", false, new byte[0], new byte[0], 30, 100f));
			
			// Send a new event to the heading event reader
			_readerService.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					_headingEventReaderId, new TFPos(80, 70), "stream", 13, false, Guid.NewGuid(),
					"type", false, new byte[0], new byte[0]));
		}

		[Test]
		public void it_does_not_get_the_event_before_the_checkpoint() {
			Assert.AreEqual(false, _subscription.ReceivedEvents
				.Any(v => v.Data.Position.PreparePosition <= 10));
		}
		
		[Test]
		public void it_gets_the_event_after_the_checkpoint_and_before_subscribing_to_the_heading_event_reader() {
			Assert.AreEqual(true, _subscription.ReceivedEvents
				.Any(v => v.Data.Position.PreparePosition == 30));
		}

		[Test]
		public void it_gets_the_events_after_subscribing() {
			Assert.AreEqual(true, _subscription.ReceivedEvents
				.Any(v => v.Data.Position.PreparePosition == 50 
				          || v.Data.Position.PreparePosition == 70));
		}
	}
}
