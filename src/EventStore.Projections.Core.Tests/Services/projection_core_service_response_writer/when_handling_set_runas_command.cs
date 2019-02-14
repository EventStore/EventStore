using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Responses;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_response_writer {
	[TestFixture]
	class
		when_handling_set_runas_command : specification_with_projection_core_service_response_writer {
		private string _name;
		private ProjectionManagementMessage.RunAs _runAs;
		private ProjectionManagementMessage.Command.SetRunAs.SetRemove _setRemove;

		protected override void Given() {
			_name = "name";
			_runAs = ProjectionManagementMessage.RunAs.System;
			_setRemove = ProjectionManagementMessage.Command.SetRunAs.SetRemove.Remove;
		}

		protected override void When() {
			_sut.Handle(
				new ProjectionManagementMessage.Command.SetRunAs(new NoopEnvelope(), _name, _runAs, _setRemove));
		}

		[Test]
		public void publishes_set_runas_command() {
			var command = AssertParsedSingleCommand<SetRunAsCommand>("$set-runas");
			Assert.AreEqual(_name, command.Name);
			Assert.AreEqual(_runAs, (ProjectionManagementMessage.RunAs)command.RunAs);
			Assert.AreEqual(_setRemove, command.SetRemove);
		}
	}
}
