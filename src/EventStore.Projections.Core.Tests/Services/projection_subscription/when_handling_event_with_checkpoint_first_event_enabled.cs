using System;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_subscription {
	[TestFixture]
	public class when_handling_event_with_checkpoint_first_event_enabled : TestFixtureWithProjectionSubscription {
		protected override void Given() {
			_checkpointFirstEvent = true;
		}

		protected override void When() {
			_subscription.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					Guid.NewGuid(), new TFPos(200, 200), "test-stream", 1, false, Guid.NewGuid(),
					"bad-event-type", false, new byte[0], new byte[0]));
		}

		[Test]
		public void checkpoint_is_suggested() {
			Assert.AreEqual(1, _checkpointHandler.HandledMessages.Count);
		}
	}
	
	[TestFixture]
	public class when_handling_event_that_does_not_pass_filter_with_checkpoint_first_event_enabled
		: TestFixtureWithProjectionSubscription {
		protected override void Given() {
			_checkpointFirstEvent = true;
			_source = source => {
				source.FromAll();
				source.IncludeEvent("specific-event");
			};
		}

		protected override void When() {
			_subscription.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					Guid.NewGuid(), new TFPos(200, 200), "test-stream", 1, false, Guid.NewGuid(),
					"bad-event-type", false, new byte[0], new byte[0]));
		}

		[Test]
		public void checkpoint_is_suggested() {
			Assert.AreEqual(1, _checkpointHandler.HandledMessages.Count);
		}
	}
	
	[TestFixture]
	public class when_handling_event_that_does_not_belong_to_stream_with_checkpoint_first_event_enabled
		: TestFixtureWithProjectionSubscription {
		protected override void Given() {
			_checkpointFirstEvent = true;
			_source = source => {
				source.FromStream("test-stream");
				source.IncludeEvent("specific-event");
			};
		}

		protected override void When() {
			_subscription.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					Guid.NewGuid(), new TFPos(200, 200), "bad-stream", 1, false, Guid.NewGuid(),
					"bad-event-type", false, new byte[0], new byte[0]));
		}

		[Test]
		public void checkpoint_is_not_suggested() {
			Assert.IsEmpty(_checkpointHandler.HandledMessages);
		}
	}
}
