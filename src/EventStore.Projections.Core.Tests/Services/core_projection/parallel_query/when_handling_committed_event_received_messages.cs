using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.core_projection.parallel_query {
	[TestFixture]
	class when_handling_committed_event_received_messages : specification_with_parallel_query {
		protected override void Given() {
			base.Given();
			_eventId = Guid.NewGuid();
		}

		protected override void When() {
			var tag0 = CheckpointTag.FromByStreamPosition(0, "", 0, null, -1, 10000);
			var tag1 = CheckpointTag.FromByStreamPosition(0, "", 1, null, -1, 10000);
			_bus.Publish(
				EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
					new ResolvedEvent(
						"catalog", 0, "catalog", 0, false, new TFPos(120, 110), _eventId, "$@", false, "test-stream",
						""),
					tag0, _subscriptionId, 0));
			_bus.Publish(
				EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
					new ResolvedEvent(
						"catalog", 1, "catalog", 1, false, new TFPos(220, 210), Guid.NewGuid(), "$@", false,
						"test-stream2", ""), tag1, _subscriptionId, 1));
		}

		[Test]
		public void spools_slave_stream_processing() {
			var spooled = HandledMessages.OfType<ReaderSubscriptionManagement.SpoolStreamReading>().ToArray();
			Assert.AreEqual(2, spooled.Length);
		}
	}
}
