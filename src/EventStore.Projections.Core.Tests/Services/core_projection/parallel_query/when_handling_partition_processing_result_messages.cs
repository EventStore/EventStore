using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.core_projection.parallel_query {
	[TestFixture]
	class when_handling_partition_processing_result_messages : specification_with_parallel_query {
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
						"catalog",
						0,
						"catalog",
						0,
						false,
						new TFPos(120, 110),
						_eventId,
						"$@",
						false,
						"account-00",
						""),
					tag0,
					_subscriptionId,
					0));
			_bus.Publish(
				EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
					new ResolvedEvent(
						"catalog",
						1,
						"catalog",
						1,
						false,
						new TFPos(220, 210),
						Guid.NewGuid(),
						"$@",
						false,
						"account-01",
						""),
					tag1,
					_subscriptionId,
					1));
			var spoolRequests = HandledMessages.OfType<ReaderSubscriptionManagement.SpoolStreamReading>().ToArray();

			_bus.Publish(
				new PartitionProcessingResult(
					_workerId,
					_masterProjectionId,
					spoolRequests[0].SubscriptionId,
					"account-00",
					Guid.Empty,
					CheckpointTag.FromByStreamPosition(0, "", 0, "account-00", long.MaxValue, 10000),
					"{\"data\":1}"));
			_bus.Publish(
				new PartitionProcessingResult(
					_workerId,
					_masterProjectionId,
					spoolRequests[1].SubscriptionId,
					"account-01",
					Guid.Empty,
					CheckpointTag.FromByStreamPosition(0, "", 1, "account-01", long.MaxValue, 10000),
					"{\"data\":2}"));
		}

		[Test]
		public void writes_state_for_each_partition() {
			Assert.AreEqual(2, _writeEventHandler.HandledMessages.OfEventType("Result").Count);
			var message = _writeEventHandler.HandledMessages.WithEventType("Result")[0];
			Assert.AreEqual("$projections-projection-account-00-result", message.EventStreamId);
			var message2 = _writeEventHandler.HandledMessages.WithEventType("Result")[1];
			Assert.AreEqual("$projections-projection-account-01-result", message2.EventStreamId);
		}
	}
}
