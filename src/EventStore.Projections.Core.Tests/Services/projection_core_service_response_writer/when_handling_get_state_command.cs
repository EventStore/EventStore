using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Responses;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_response_writer {
	[TestFixture]
	class when_handling_get_state_command : specification_with_projection_core_service_response_writer {
		private string _name;
		private string _partition;

		protected override void Given() {
			_name = "name";
			_partition = "partition";
		}

		protected override void When() {
			_sut.Handle(new ProjectionManagementMessage.Command.GetState(new NoopEnvelope(), _name, _partition));
		}

		[Test]
		public void publishes_get_state_command() {
			var command = AssertParsedSingleCommand<GetStateCommand>("$get-state");
			Assert.AreEqual(_name, command.Name);
			Assert.AreEqual(_partition, command.Partition);
		}
	}
}
