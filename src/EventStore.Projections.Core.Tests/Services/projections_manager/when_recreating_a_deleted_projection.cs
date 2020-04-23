using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager {
	[TestFixture]
	public class when_recreating_a_deleted_projection : TestFixtureWithProjectionCoreAndManagementServices {
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
					enabled: true, checkpointsEnabled: true, emitEnabled: true, trackEmittedStreams: true);
			yield return
				new ProjectionManagementMessage.Command.Disable(
					new PublishEnvelope(_bus), _projectionName, ProjectionManagementMessage.RunAs.System);
			yield return
				new ProjectionManagementMessage.Command.Delete(
					new PublishEnvelope(_bus), _projectionName,
					ProjectionManagementMessage.RunAs.System, true, true, false);
			yield return
				new ProjectionManagementMessage.Command.Post(
					new PublishEnvelope(_bus), ProjectionMode.Continuous, _projectionName,
					ProjectionManagementMessage.RunAs.System, "JS", @"fromAll().when({$any:function(s,e){return s;}});",
					enabled: true, checkpointsEnabled: true, emitEnabled: true, trackEmittedStreams: true);
		}

		[Test, Category("v8")]
		public void a_projection_created_event_should_be_written() {
			Assert.AreEqual(
				ProjectionEventTypes.ProjectionCreated,
				_consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().First().Events[0].EventType);
			Assert.AreEqual(
				_projectionName,
				Helper.UTF8NoBom.GetString(_consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().First()
					.Events[0].Data));
		}

		[Test, Category("v8")]
		public void it_can_be_listed() {
			_manager.Handle(
				new ProjectionManagementMessage.Command.GetStatistics(new PublishEnvelope(_bus), null, null, false));

			Assert.AreEqual(
				1,
				_consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Count(
					v => v.Projections.Any(p => p.Name == _projectionName)));
		}
	}
}
