using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Responses;
using EventStore.Projections.Core.Services;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_response_writer {
	[TestFixture]
	class when_handling_get_statistics_command : specification_with_projection_core_service_response_writer {
		private string _name;
		private ProjectionMode? _mode;
		private bool _includeDeleted;

		protected override void Given() {
			_name = "name";
			_mode = ProjectionMode.Continuous;
			_includeDeleted = true;
		}

		protected override void When() {
			_sut.Handle(
				new ProjectionManagementMessage.Command.GetStatistics(new NoopEnvelope(), _mode, _name,
					_includeDeleted));
		}

		[Test]
		public void publishes_get_statistics_command() {
			var command = AssertParsedSingleCommand<GetStatisticsCommand>("$get-statistics");
			Assert.AreEqual(_name, command.Name);
			Assert.AreEqual(_mode, command.Mode);
			Assert.AreEqual(_includeDeleted, command.IncludeDeleted);
		}
	}
}
