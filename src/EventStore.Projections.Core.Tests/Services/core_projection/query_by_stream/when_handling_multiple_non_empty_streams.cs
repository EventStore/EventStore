using System;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.core_projection.query_by_stream {
	[TestFixture]
	[Ignore("This isn't implemented yet")]
	public class when_handling_multiple_non_empty_streams : specification_with_from_catalog_query {
		protected override void When() {
			//projection subscribes here
			_eventId = Guid.NewGuid();
			_consumer.HandledMessages.Clear();

			var tag0 = CheckpointTag.FromByStreamPosition(0, "catalog", 0, "account-00", 0, long.MinValue);
			_bus.Publish(
				EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
					new ResolvedEvent(
						"account-00", 0, "account-00", 0, false, new TFPos(120, 110), _eventId,
						"handle_this_type", false, "data", "metadata"), tag0, _subscriptionId, 0));
			_bus.Publish(
				new EventReaderSubscriptionMessage.PartitionEofReached(
					_subscriptionId, tag0,
					"account-00", 1));


			var tag1 = CheckpointTag.FromByStreamPosition(0, "catalog", 1, "account-01", 0, long.MinValue);
			_bus.Publish(
				EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
					new ResolvedEvent(
						"account-01", 0, "account-01", 0, false, new TFPos(220, 210), _eventId,
						"handle_this_type", false, "data", "metadata"), tag1, _subscriptionId, 2));
			_bus.Publish(
				new EventReaderSubscriptionMessage.PartitionEofReached(
					_subscriptionId, tag1,
					"account-01", 3));
		}

		[Test]
		public void writes_empty_state_for_each_partition() {
			Assert.AreEqual(2, _writeEventHandler.HandledMessages.OfEventType("Result").Count);
			var message = _writeEventHandler.HandledMessages.WithEventType("Result")[0];
			Assert.AreEqual("$projections-projection-account-00-result", message.EventStreamId);
			var message2 = _writeEventHandler.HandledMessages.WithEventType("Result")[1];
			Assert.AreEqual("$projections-projection-account-01-result", message2.EventStreamId);
		}
	}
}
