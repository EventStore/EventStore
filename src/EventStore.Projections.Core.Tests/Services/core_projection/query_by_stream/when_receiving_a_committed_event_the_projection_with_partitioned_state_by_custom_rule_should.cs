using System;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.query_by_stream {
	[TestFixture]
	public class when_handling_immediate_eof : specification_with_from_catalog_query {
		protected override void When() {
			//projection subscribes here
			_eventId = Guid.NewGuid();
			_consumer.HandledMessages.Clear();
			_bus.Publish(
				new EventReaderSubscriptionMessage.EofReached(
					_subscriptionId, CheckpointTag.FromByStreamPosition(0, "catalog", -1, null, -1, long.MinValue), 0));
		}

		[Test]
		public void does_not_write_any_result_event() {
			Assert.AreEqual(0, _writeEventHandler.HandledMessages.OfEventType("Result").Count);
			//var message = _writeEventHandler.HandledMessages.WithEventType("Result")[0];
			//Assert.AreEqual("$projections-projection-result", message.EventStreamId);
		}
	}
}
