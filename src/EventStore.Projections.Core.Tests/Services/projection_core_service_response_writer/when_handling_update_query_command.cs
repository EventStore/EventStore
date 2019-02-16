using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Responses;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_response_writer {
	[TestFixture]
	class
		when_handling_update_query_command : specification_with_projection_core_service_response_writer {
		private string _name;
		private ProjectionManagementMessage.RunAs _runAs;
		private string _handlerType;
		private string _query;
		private bool? _emitEnabled;

		protected override void Given() {
			_name = "name";
			_runAs = ProjectionManagementMessage.RunAs.System;
			_handlerType = "JS";
			_query = "fromAll()";
			_emitEnabled = true;
		}

		protected override void When() {
			_sut.Handle(
				new ProjectionManagementMessage.Command.UpdateQuery(
					new NoopEnvelope(),
					_name,
					_runAs,
					_handlerType,
					_query,
					_emitEnabled));
		}

		[Test]
		public void publishes_update_query_command() {
			var command = AssertParsedSingleCommand<UpdateQueryCommand>("$update-query");
			Assert.AreEqual(_name, command.Name);
			Assert.AreEqual(_runAs, (ProjectionManagementMessage.RunAs)command.RunAs);
			Assert.AreEqual(_handlerType, command.HandlerType);
			Assert.AreEqual(_query, command.Query);
			Assert.AreEqual(_emitEnabled, command.EmitEnabled);
		}
	}
}
