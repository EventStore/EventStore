using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Responses;
using EventStore.Projections.Core.Services;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_response_writer {
	[TestFixture]
	class when_handling_post_command : specification_with_projection_core_service_response_writer {
		private string _name;
		private ProjectionManagementMessage.RunAs _runAs;
		private ProjectionMode _mode;
		private string _handlerType;
		private string _query;
		private bool _enabled;
		private bool _checkpointsEnabled;
		private bool _emitEnabled;
		private bool _enableRunAs;
		private bool _trackEmittedStreams;

		protected override void Given() {
			_name = "name";
			_runAs = ProjectionManagementMessage.RunAs.System;
			_mode = ProjectionMode.Continuous;
			_handlerType = "JS";
			_query = "fromAll();";
			_enabled = true;
			_checkpointsEnabled = true;
			_emitEnabled = true;
			_enableRunAs = true;
			_trackEmittedStreams = true;
		}

		protected override void When() {
			_sut.Handle(
				new ProjectionManagementMessage.Command.Post(
					new NoopEnvelope(),
					_mode,
					_name,
					_runAs,
					_handlerType,
					_query,
					_enabled,
					_checkpointsEnabled,
					_emitEnabled,
					_trackEmittedStreams,
					_enableRunAs));
		}

		[Test]
		public void publishes_post_command() {
			var command = AssertParsedSingleCommand<PostCommand>("$post");
			Assert.AreEqual(_name, command.Name);
			Assert.AreEqual(_runAs, (ProjectionManagementMessage.RunAs)command.RunAs);
			Assert.AreEqual(_mode, command.Mode);
			Assert.AreEqual(_handlerType, command.HandlerType);
			Assert.AreEqual(_query, command.Query);
			Assert.AreEqual(_enabled, command.Enabled);
			Assert.AreEqual(_checkpointsEnabled, command.CheckpointsEnabled);
			Assert.AreEqual(_emitEnabled, command.EmitEnabled);
			Assert.AreEqual(_trackEmittedStreams, command.TrackEmittedStreams);
			Assert.AreEqual(_enableRunAs, command.EnableRunAs);
		}
	}
}
