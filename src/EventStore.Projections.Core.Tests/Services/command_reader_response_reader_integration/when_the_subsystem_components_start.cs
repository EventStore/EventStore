using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Tests.Services.projections_manager;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EventStore.Projections.Core.Tests.Services.command_reader_response_reader_integration {
	[TestFixture]
	public class when_the_subsystem_components_start : TestFixtureWithProjectionCoreAndManagementServices {
		private Guid _uniqueId;

		protected override void Given() {
			AllWritesSucceed();
		}

		protected override IEnumerable<WhenStep> When() {
			_uniqueId = Guid.NewGuid();
			yield return new ProjectionSubsystemMessage.StartComponents(_uniqueId);
		}

		[Test]
		public void readers_should_use_the_same_control_stream_id() {
			Assert.AreEqual(_uniqueId,
				_consumer.HandledMessages.OfType<ProjectionManagementMessage.Starting>().First().InstanceCorrelationId);
			Assert.AreEqual(_uniqueId,
				_consumer.HandledMessages.OfType<ProjectionCoreServiceMessage.StartCore>().First().InstanceCorrelationId);
		}
	}
}
