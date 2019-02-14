using System;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Responses;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_response_writer {
	[TestFixture]
	class when_handling_started_message : specification_with_projection_core_service_response_writer {
		private Guid _projectionId;

		protected override void Given() {
			_projectionId = Guid.NewGuid();
		}

		protected override void When() {
			_sut.Handle(new CoreProjectionStatusMessage.Started(_projectionId));
		}

		[Test]
		public void publishes_started_response() {
			var command = AssertParsedSingleCommand<Started>("$started");
			Assert.AreEqual(_projectionId.ToString("N"), command.Id);
		}
	}
}
