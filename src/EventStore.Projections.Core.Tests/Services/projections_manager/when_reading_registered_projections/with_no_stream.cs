﻿using System;
using System.Linq;
using EventStore.Core.Messages;
using NUnit.Framework;
using EventStore.Projections.Core.Services.Processing;
using System.Collections.Generic;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.when_reading_registered_projections {
	[TestFixture]
	public class with_no_stream : TestFixtureWithProjectionCoreAndManagementServices {
		protected override void Given() {
			AllWritesSucceed();
			NoStream(ProjectionNamesBuilder.ProjectionsRegistrationStream);
		}

		protected override IEnumerable<WhenStep> When() {
			yield return new ProjectionSubsystemMessage.StartComponents(Guid.NewGuid());
		}

		protected override bool GivenInitializeSystemProjections() {
			return false;
		}

		[Test]
		public void it_should_write_the_projections_initialized_event() {
			Assert.AreEqual(1, _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Count(x =>
				x.EventStreamId == ProjectionNamesBuilder.ProjectionsRegistrationStream &&
				x.Events[0].EventType == ProjectionEventTypes.ProjectionsInitialized));
		}

		[Test]
		public void it_should_not_write_any_projection_created_events() {
			Assert.AreEqual(0, _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Count(x =>
				x.EventStreamId == ProjectionNamesBuilder.ProjectionsRegistrationStream &&
				x.Events[0].EventType == ProjectionEventTypes.ProjectionCreated));
		}
	}
}
