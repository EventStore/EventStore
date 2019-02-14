using System;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Responses;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_response_writer {
	[TestFixture]
	class when_handling_result_report_message : specification_with_projection_core_service_response_writer {
		private Guid _projectionId;
		private string _result;
		private string _partition;
		private Guid _correlationId;
		private CheckpointTag _position;

		protected override void Given() {
			_correlationId = Guid.NewGuid();
			_projectionId = Guid.NewGuid();
			_result = "{\"a\":1}";
			_partition = "partition";
			_position = CheckpointTag.FromStreamPosition(1, "stream", 10);
		}

		protected override void When() {
			_sut.Handle(
				new CoreProjectionStatusMessage.ResultReport(
					_correlationId,
					_projectionId,
					_partition,
					_result,
					_position));
		}

		[Test]
		public void publishes_result_report_response() {
			var command = AssertParsedSingleCommand<ResultReport>("$result");
			Assert.AreEqual(_projectionId.ToString("N"), command.Id);
			Assert.AreEqual(_correlationId.ToString("N"), command.CorrelationId);
			Assert.AreEqual(_result, command.Result);
			Assert.AreEqual(_partition, command.Partition);
			Assert.AreEqual(_position, command.Position);
		}
	}
}
