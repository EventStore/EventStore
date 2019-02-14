using System;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Commands;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.slave_projection_response_writer {
	[TestFixture]
	class when_handling_spool_stream_reading_message : specification_with_slave_projection_response_writer {
		private Guid _workerId;
		private Guid _subscriptionId;

		protected override void Given() {
			_workerId = Guid.NewGuid();
			_subscriptionId = Guid.NewGuid();
		}

		protected override void When() {
			_sut.Handle(
				new ReaderSubscriptionManagement.SpoolStreamReading(_workerId, _subscriptionId, "stream1", 100,
					1000000));
		}

		[Test]
		public void publishes_partition_measured_response() {
			var body =
				AssertParsedSingleResponse<SpoolStreamReadingCommand>(
					"$spool-stream-reading",
					_workerId);

			Assert.AreEqual(_subscriptionId.ToString("N"), body.SubscriptionId);
			Assert.AreEqual("stream1", body.StreamId);
			Assert.AreEqual(100, body.CatalogSequenceNumber);
			Assert.AreEqual(1000000, body.LimitingCommitPosition);
		}
	}
}
