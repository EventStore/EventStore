using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager {
	[TestFixture]
	public class
		when_posting_a_persistent_projection_and_writes_succeed : TestFixtureWithProjectionCoreAndManagementServices {
		protected override void Given() {
			NoStream("$projections-test-projection-order");
			AllWritesToSucceed("$projections-test-projection-order");
			NoStream("$projections-test-projection-checkpoint");
			AllWritesSucceed();
			NoOtherStreams();
		}

		private string _projectionName;

		protected override IEnumerable<WhenStep> When() {
			_projectionName = "test-projection";
			yield return new ProjectionSubsystemMessage.StartComponents(Guid.NewGuid());
			yield return
				new ProjectionManagementMessage.Command.Post(
					new PublishEnvelope(_bus), ProjectionMode.Continuous, _projectionName,
					ProjectionManagementMessage.RunAs.System, "JS", @"fromAll().when({$any:function(s,e){return s;}});",
					enabled: true, checkpointsEnabled: true, emitEnabled: true, trackEmittedStreams: true);
		}

		[Test, Category("v8")]
		public void projection_status_is_running() {
			_manager.Handle(
				new ProjectionManagementMessage.Command.GetStatistics(new PublishEnvelope(_bus), null, _projectionName,
					true));
			Assert.AreEqual(
				ManagedProjectionState.Running,
				_consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Single().Projections[0]
					.MasterStatus);
		}

		[Test, Category("v8")]
		public void a_projection_updated_event_is_written() {
			Assert.IsTrue(
				_consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Any(
					v => v.Events[0].EventType == ProjectionEventTypes.ProjectionUpdated));
		}

		[Test, Category("v8")]
		public void a_projection_updated_message_is_published() {
			// not published until writes complete
			Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.Updated>().Count());
		}
	}
}
