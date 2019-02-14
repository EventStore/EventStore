using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Responses;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_response_writer {
	[TestFixture]
	class
		when_delete_command : specification_with_projection_core_service_response_writer {
		private string _name;
		private ProjectionManagementMessage.RunAs _runAs;
		private bool _deleteCheckpointStream;
		private bool _deleteStateStream;
		private bool _deleteEmittedStreams;

		protected override void Given() {
			_name = "name";
			_runAs = ProjectionManagementMessage.RunAs.System;
			_deleteCheckpointStream = true;
			_deleteStateStream = true;
			_deleteEmittedStreams = false;
		}

		protected override void When() {
			_sut.Handle(
				new ProjectionManagementMessage.Command.Delete(
					new NoopEnvelope(),
					_name,
					_runAs,
					_deleteCheckpointStream,
					_deleteStateStream,
					_deleteEmittedStreams));
		}

		[Test]
		public void publishes_delete_command() {
			var command = AssertParsedSingleCommand<DeleteCommand>("$delete");
			Assert.AreEqual(_name, command.Name);
			Assert.AreEqual(_runAs, (ProjectionManagementMessage.RunAs)command.RunAs);
			Assert.AreEqual(_deleteCheckpointStream, command.DeleteCheckpointStream);
			Assert.AreEqual(_deleteStateStream, command.DeleteStateStream);
		}
	}
}
