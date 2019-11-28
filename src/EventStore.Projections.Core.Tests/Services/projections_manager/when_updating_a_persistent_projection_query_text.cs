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
	public class when_updating_a_persistent_projection_query_text : TestFixtureWithProjectionCoreAndManagementServices {
		protected override void Given() {
			NoStream("$projections-test-projection");
			NoStream("$projections-test-projection-result");
			NoStream("$projections-test-projection-order");
			AllWritesToSucceed("$projections-test-projection-order");
			NoStream("$projections-test-projection-checkpoint");
			AllWritesSucceed();
		}

		private string _projectionName;
		private string _newProjectionSource;

		protected override IEnumerable<WhenStep> When() {
			_projectionName = "test-projection";
			yield return (new ProjectionSubsystemMessage.StartComponents(Guid.NewGuid()));
			yield return
				(new ProjectionManagementMessage.Command.Post(
					new PublishEnvelope(_bus), ProjectionMode.Continuous, _projectionName,
					ProjectionManagementMessage.RunAs.System, "JS",
					@"fromAll(); on_any(function(){return {};});log(1);",
					enabled: true, checkpointsEnabled: true, emitEnabled: true, trackEmittedStreams: true));
			// when
			_newProjectionSource = @"fromAll().when({a:function(){return {};}});log(2);";
			yield return
				(new ProjectionManagementMessage.Command.UpdateQuery(
					new PublishEnvelope(_bus), _projectionName, ProjectionManagementMessage.RunAs.System, "JS",
					_newProjectionSource, emitEnabled: null));
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
		public void emit_enabled_options_remains_unchanged() {
			_manager.Handle(
				new ProjectionManagementMessage.Command.GetQuery(
					new PublishEnvelope(_bus), _projectionName, ProjectionManagementMessage.RunAs.Anonymous));
			Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionQuery>().Count());
			var projectionQuery =
				_consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionQuery>().Single();
			Assert.AreEqual(_projectionName, projectionQuery.Name);
			Assert.AreEqual(true, projectionQuery.EmitEnabled);
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
			Assert.AreEqual( // empty? why?
				"", _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Single().State);
		}

		[Test, Category("v8")]
		public void correct_query_has_been_prepared() {
			var lastPrepared = _consumer.HandledMessages.OfType<CoreProjectionStatusMessage.Prepared>().LastOrDefault();
			Assert.IsNotNull(lastPrepared);
			Assert.IsFalse(lastPrepared.SourceDefinition.AllEvents);
		}
	}
}
