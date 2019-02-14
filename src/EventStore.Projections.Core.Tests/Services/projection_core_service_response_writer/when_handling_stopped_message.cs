using System;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Responses;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_response_writer {
	[TestFixture]
	class when_handling_stopped_message : specification_with_projection_core_service_response_writer {
		private Guid _projectionId;
		private bool _completed;
		private string _projectionName;

		protected override void Given() {
			_projectionId = Guid.NewGuid();
			_completed = true;
			_projectionName = Guid.NewGuid().ToString();
		}

		protected override void When() {
			_sut.Handle(new CoreProjectionStatusMessage.Stopped(_projectionId, _projectionName, _completed));
		}

		[Test]
		public void publishes_stopped_response() {
			var command = AssertParsedSingleCommand<Stopped>("$stopped");
			Assert.AreEqual(_projectionId.ToString("N"), command.Id);
			Assert.AreEqual(_completed, command.Completed);
		}
	}
}
