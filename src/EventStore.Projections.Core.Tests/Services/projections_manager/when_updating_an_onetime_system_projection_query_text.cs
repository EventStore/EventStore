// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Messaging;
using EventStore.Core.Tests;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class when_updating_an_onetime_system_projection_query_text<TLogFormat, TStreamId> : TestFixtureWithProjectionCoreAndManagementServices<TLogFormat, TStreamId> {
		private string _projectionName;
		private string _newProjectionSource;

		protected override void Given() {
			NoOtherStreams();
		}
		
		protected override IEnumerable<WhenStep> When() {
			_projectionName = "$by_correlation_id";
			yield return (new ProjectionSubsystemMessage.StartComponents(Guid.NewGuid()));
			yield return
				(new ProjectionManagementMessage.Command.Post(
					_bus, ProjectionMode.Transient, _projectionName,
					ProjectionManagementMessage.RunAs.Anonymous, "native:EventStore.Projections.Core.Standard.ByCorrelationId", "{\"correlationIdProperty\":\"$myCorrelationId\"}",
					enabled: true, checkpointsEnabled: false, emitEnabled: false, trackEmittedStreams: true));
			// when
			_newProjectionSource = "{\"correlationIdProperty\":\"$updateCorrelationId\"}";
			yield return
				(new ProjectionManagementMessage.Command.UpdateQuery(
					_bus, _projectionName, ProjectionManagementMessage.RunAs.Anonymous,
					_newProjectionSource, emitEnabled: null));
		}

		[Test, Category("v8")]
		public void the_projection_source_can_be_retrieved() {
			_manager.Handle(
				new ProjectionManagementMessage.Command.GetQuery(
					_bus, _projectionName, ProjectionManagementMessage.RunAs.Anonymous));
			Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionQuery>().Count());
			var projectionQuery =
				_consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionQuery>().Single();
			Assert.AreEqual(_projectionName, projectionQuery.Name);
			Assert.AreEqual(_newProjectionSource, projectionQuery.Query);
		}

		[Test, Category("v8")]
		public void the_projection_status_is_still_running() {
			_manager.Handle(
				new ProjectionManagementMessage.Command.GetStatistics(_bus, null, _projectionName,
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
					.LeaderStatus);
		}

		[Test, Category("v8")]
		public void the_projection_state_can_be_retrieved() {
			_manager.Handle(
				new ProjectionManagementMessage.Command.GetState(_bus, _projectionName, ""));
			_queue.Process();

			Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Count());
			Assert.AreEqual(
				_projectionName,
				_consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Single().Name);
			Assert.AreEqual(
				"", _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Single().State);
		}
	}
}
