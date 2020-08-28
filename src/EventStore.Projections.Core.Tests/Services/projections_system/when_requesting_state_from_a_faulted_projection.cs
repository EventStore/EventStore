using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using NUnit.Framework;
using System.Linq;
using EventStore.Core.TransactionLog.Data;

namespace EventStore.Projections.Core.Tests.Services.projections_system {
	[TestFixture]
	class when_requesting_state_from_a_faulted_projection : with_projection_config {
		private TFPos _message1Position;

		protected override void Given() {
			base.Given();
			NoOtherStreams();
			_message1Position = ExistingEvent("stream1", "message1", null, "{}");

			_projectionSource = @"fromAll().when({message1: function(s,e){ throw 1; }});";
		}

		protected override IEnumerable<WhenStep> When() {
			yield return
				new ProjectionManagementMessage.Command.Post(
					Envelope, ProjectionMode.Continuous, _projectionName, ProjectionManagementMessage.RunAs.System,
					"js",
					_projectionSource, enabled: true, checkpointsEnabled: true, emitEnabled: true,
					trackEmittedStreams: true);
			yield return Yield;
			yield return new ProjectionManagementMessage.Command.GetState(Envelope, _projectionName, "");
		}

		protected override bool GivenStartSystemProjections() {
			return true;
		}

		[Test]
		public void reported_state_is_before_the_fault_position() {
			var states = HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().ToArray();
			Assert.AreEqual(1, states.Length);
			var state = states[0];

			Assert.That(state.Position.Streams.Count == 1);
			Assert.That(state.Position.Streams.Keys.First() == "message1");
			Assert.That(state.Position.Streams["message1"] == -1);
			Assert.That(
				state.Position.Position <= _message1Position, "{0} <= {1}", state.Position.Position, _message1Position);
		}
	}
}
