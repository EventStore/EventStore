using System;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Commands;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.command_writer {
	[TestFixture]
	class when_handling_get_state_message : specification_with_projection_manager_command_writer {
		private Guid _projectionId;
		private Guid _correlationId;
		private string _partition;
		private Guid _workerId;

		protected override void Given() {
			_projectionId = Guid.NewGuid();
			_correlationId = Guid.NewGuid();
			_partition = "partition";
			_workerId = Guid.NewGuid();
		}

		protected override void When() {
			_sut.Handle(
				new CoreProjectionManagementMessage.GetState(_correlationId, _projectionId, _partition, _workerId));
		}

		[Test]
		public void publishes_get_state_command() {
			var command = AssertParsedSingleCommand<GetStateCommand>(
				"$get-state",
				_workerId);
			Assert.AreEqual(_projectionId.ToString("N"), command.Id);
			Assert.AreEqual(_correlationId.ToString("N"), command.CorrelationId);
			Assert.AreEqual(_partition, command.Partition);
		}
	}
}
