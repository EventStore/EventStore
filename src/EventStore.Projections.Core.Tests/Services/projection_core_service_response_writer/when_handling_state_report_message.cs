using System;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Responses;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_response_writer {
	[TestFixture]
	class when_handling_state_report_message : specification_with_projection_core_service_response_writer {
		private Guid _projectionId;
		private string _state;
		private string _partition;
		private Guid _correlationId;
		private CheckpointTag _position;

		protected override void Given() {
			_correlationId = Guid.NewGuid();
			_projectionId = Guid.NewGuid();
			_state = "{\"a\":1}";
			_partition = "partition";
			_position = CheckpointTag.FromStreamPosition(1, "stream", 10);
		}

		protected override void When() {
			_sut.Handle(
				new CoreProjectionStatusMessage.StateReport(
					_correlationId,
					_projectionId,
					_partition,
					_state,
					_position));
		}

		[Test]
		public void publishes_state_report_response() {
			var command = AssertParsedSingleCommand<StateReport>("$state");
			Assert.AreEqual(_projectionId.ToString("N"), command.Id);
			Assert.AreEqual(_correlationId.ToString("N"), command.CorrelationId);
			Assert.AreEqual(_state, command.State);
			Assert.AreEqual(_partition, command.Partition);
			Assert.AreEqual(_position, command.Position);
		}
	}
}
