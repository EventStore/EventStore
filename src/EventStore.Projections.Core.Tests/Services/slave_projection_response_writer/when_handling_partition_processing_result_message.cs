using System;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;
using EventStore.Projections.Core.Messages.Persisted.Responses.Slave;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.slave_projection_response_writer {
	[TestFixture]
	class when_handling_partition_processing_result_message : specification_with_slave_projection_response_writer {
		private Guid _workerId;
		private Guid _masterProjectionId;
		private Guid _subscriptionId;
		private string _partition;
		private Guid _causedBy;
		private CheckpointTag _position;
		private string _result;

		protected override void Given() {
			_workerId = Guid.NewGuid();
			_masterProjectionId = Guid.NewGuid();
			_subscriptionId = Guid.NewGuid();
			_partition = "partition";
			_causedBy = Guid.NewGuid();
			_position = CheckpointTag.FromStreamPosition(1, "stream", 1);
			_result = "{}";
		}

		protected override void When() {
			_sut.Handle(
				new PartitionProcessingResultOutput(
					_workerId,
					_masterProjectionId,
					_subscriptionId,
					_partition,
					_causedBy,
					_position,
					_result));
		}

		[Test]
		public void publishes_partition_processing_result_response() {
			var body = AssertParsedSingleResponse<PartitionProcessingResultResponse>("$result", _masterProjectionId);
			Assert.AreEqual(_subscriptionId.ToString("N"), body.SubscriptionId);
			Assert.AreEqual(_partition, body.Partition);
			Assert.AreEqual(_causedBy.ToString("N"), body.CausedBy);
			Assert.AreEqual(_position, body.Position);
			Assert.AreEqual(_result, body.Result);
		}
	}
}
