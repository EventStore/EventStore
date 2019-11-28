using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager {
	[TestFixture]
	public class
		when_deleting_a_persistent_projection_and_not_authorised : TestFixtureWithProjectionCoreAndManagementServices {
		private string _projectionName;

		protected override void Given() {
			_projectionName = "test-projection";
			AllWritesSucceed();
			NoOtherStreams();
		}

		protected override IEnumerable<WhenStep> When() {
			yield return new ProjectionSubsystemMessage.StartComponents(Guid.NewGuid());
			yield return
				new ProjectionManagementMessage.Command.Post(
					new PublishEnvelope(_bus), ProjectionMode.Continuous, _projectionName,
					ProjectionManagementMessage.RunAs.System, "JS", @"fromAll().when({$any:function(s,e){return s;}});",
					enabled: true, checkpointsEnabled: true, emitEnabled: false, trackEmittedStreams: true);
			yield return
				new ProjectionManagementMessage.Command.Disable(
					new PublishEnvelope(_bus), _projectionName, ProjectionManagementMessage.RunAs.System);
			yield return
				new ProjectionManagementMessage.Command.Delete(
					new PublishEnvelope(_bus), _projectionName,
					ProjectionManagementMessage.RunAs.Anonymous, false, false, false);
		}

		[Test, Category("v8")]
		public void a_projection_deleted_event_is_not_written() {
			Assert.AreNotEqual(
				ProjectionEventTypes.ProjectionDeleted,
				_consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Last().Events[0].EventType,
				$"{ProjectionEventTypes.ProjectionDeleted} event was not supposed to be written");
		}
	}
}
