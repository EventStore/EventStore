using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager {
	[TestFixture]
	public class when_updating_an_onetime_projection_query_text : TestFixtureWithProjectionCoreAndManagementServices {
		private string _projectionName;
		private string _newProjectionSource;

		protected override void Given() {
			NoOtherStreams();
		}

		[Test, Category("v8")]
		public void the_projection_source_can_be_retrieved() {
			_manager.Handle(
				new ProjectionManagementMessage.Command.GetQuery(
					new PublishEnvelope(_bus), _projectionName, ProjectionManagementMessage.RunAs.Anonymous));
			Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionQuery>().Count());
			var projectionQuery =
				_consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionQuery>().Single();
			Assert.AreEqual(_projectionName, projectionQuery.Name);
			Assert.AreEqual(_newProjectionSource, projectionQuery.Query);
		}

		[Test, Category("v8")]
		public void the_projection_status_is_still_running() {
			_manager.Handle(
				new ProjectionManagementMessage.Command.GetStatistics(new PublishEnvelope(_bus), null, _projectionName,
					false));

			Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Count());
			Assert.AreEqual(
				1,
				_consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Single().Projections.Length);
			Assert.AreEqual(
				_projectionName,
				_consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Single().Projections.Single()
					.Name);
			Assert.AreEqual(
				ManagedProjectionState.Running,
				_consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Single().Projections.Single()
					.MasterStatus);
		}

		[Test, Category("v8")]
		public void the_projection_state_can_be_retrieved() {
			_manager.Handle(
				new ProjectionManagementMessage.Command.GetState(new PublishEnvelope(_bus), _projectionName, ""));
			_queue.Process();

			Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Count());
			Assert.AreEqual(
				_projectionName,
				_consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Single().Name);
			Assert.AreEqual(
				"", _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Single().State);
		}

		protected override IEnumerable<WhenStep> When() {
			_projectionName = "test-projection";
			yield return (new ProjectionSubsystemMessage.StartComponents(Guid.NewGuid()));
			yield return
				(new ProjectionManagementMessage.Command.Post(
					new PublishEnvelope(_bus), ProjectionMode.Transient, _projectionName,
					ProjectionManagementMessage.RunAs.Anonymous, "JS", @"fromAll(); on_any(function(){});log(1);",
					enabled: true, checkpointsEnabled: false, emitEnabled: false, trackEmittedStreams: true));
			// when
			_newProjectionSource = @"fromAll(); on_any(function(){});log(2);";
			yield return
				(new ProjectionManagementMessage.Command.UpdateQuery(
					new PublishEnvelope(_bus), _projectionName, ProjectionManagementMessage.RunAs.Anonymous, "JS",
					_newProjectionSource, emitEnabled: null));
		}
	}
}
