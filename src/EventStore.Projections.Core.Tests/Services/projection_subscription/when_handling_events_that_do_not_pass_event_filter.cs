using System;
using System.Text;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;
using EventStore.Core.Tests.Services.TimeService;
using EventStore.Core.TransactionLog.Data;

namespace EventStore.Projections.Core.Tests.Services.projection_subscription {
	[TestFixture]
	public class
		when_handling_events_that_exceed_unhandled_bytes_threshold_and_checkpoint_after_reached :
			TestFixtureWithProjectionSubscription {
		protected override void Given() {
			_source = source => {
				source.FromAll();
				source.IncludeEvent("specific-event");
			};
			_checkpointAfterMs = 1000;
			_checkpointProcessedEventsThreshold = 1;
			_checkpointUnhandledBytesThreshold = 4096;
			_timeProvider = new FakeTimeProvider();
		}

		protected override void When() {
			var firstPosition = new TFPos(200, 200);	
			var secondPosition = new TFPos(
				firstPosition.CommitPosition + _checkpointUnhandledBytesThreshold + 1, 
				firstPosition.PreparePosition + _checkpointUnhandledBytesThreshold + 1);
			
			_subscription.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					Guid.NewGuid(), firstPosition, "test-stream", 1, false, Guid.NewGuid(),
					"bad-event-type", false, new byte[0], new byte[0]));
			_checkpointHandler.HandledMessages.Clear();
			((FakeTimeProvider)_timeProvider).AddToUtcTime(TimeSpan.FromMilliseconds(_checkpointAfterMs + 1));
			_subscription.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					Guid.NewGuid(), secondPosition, "test-stream", 1, false, Guid.NewGuid(),
					"bad-event-type", false, new byte[0], new byte[0]));
		}

		[Test]
		public void checkpoint_is_suggested() {
			Assert.AreEqual(1, _checkpointHandler.HandledMessages.Count);
		}
	}
	
	[TestFixture]
	public class
		when_handling_events_where_checkpoint_after_reached_does_not_exceed_unhandled_bytes_threshold :
			TestFixtureWithProjectionSubscription {
		protected override void Given() {
			_source = source => {
				source.FromAll();
				source.IncludeEvent("specific-event");
			};
			_checkpointAfterMs = 1000;
			_checkpointProcessedEventsThreshold = 1;
			_checkpointUnhandledBytesThreshold = 4096;
			_timeProvider = new FakeTimeProvider();
		}

		protected override void When() {
			var firstPosition = new TFPos(200, 200);	
			var secondPosition = new TFPos(
				firstPosition.CommitPosition + _checkpointUnhandledBytesThreshold - 1, 
				firstPosition.PreparePosition + _checkpointUnhandledBytesThreshold - 1);
			
			_subscription.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					Guid.NewGuid(), firstPosition, "test-stream", 1, false, Guid.NewGuid(),
					"bad-event-type", false, new byte[0], new byte[0]));
			_checkpointHandler.HandledMessages.Clear();
			((FakeTimeProvider)_timeProvider).AddToUtcTime(TimeSpan.FromMilliseconds(_checkpointAfterMs + 1));
			_subscription.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					Guid.NewGuid(), secondPosition, "test-stream", 1, false, Guid.NewGuid(),
					"bad-event-type", false, new byte[0], new byte[0]));
		}

		[Test]
		public void checkpoint_is_not_suggested() {
			Assert.AreEqual(0, _checkpointHandler.HandledMessages.Count);
		}
	}
}
