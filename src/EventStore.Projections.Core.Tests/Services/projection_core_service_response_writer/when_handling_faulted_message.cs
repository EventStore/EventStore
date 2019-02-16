using System;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Responses;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_response_writer {
	[TestFixture]
	class when_handling_faulted_message : specification_with_projection_core_service_response_writer {
		private Guid _projectionId;
		private string _faultedReason;

		protected override void Given() {
			_projectionId = Guid.NewGuid();
			_faultedReason = "reason";
		}

		protected override void When() {
			_sut.Handle(new CoreProjectionStatusMessage.Faulted(_projectionId, _faultedReason));
		}

		[Test]
		public void publishes_faulted_response() {
			var command = AssertParsedSingleCommand<Faulted>("$faulted");
			Assert.AreEqual(_projectionId.ToString("N"), command.Id);
			Assert.AreEqual(_faultedReason, command.FaultedReason);
		}
	}
}
