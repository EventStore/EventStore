using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_command_reader {
	[TestFixture]
	public class
		when_receiving_load_stopped_command : specification_with_projection_core_service_command_reader_started {
		private Guid _projectionId;

		protected override IEnumerable<WhenStep> When() {
			_projectionId = Guid.NewGuid();
			yield return
				CreateWriteEvent(
					"$projections-$" + _serviceId,
					"$load-stopped",
					"{\"id\":\"" + _projectionId.ToString("N") + "\"}",
					null,
					true);
		}

		[Test]
		public void publishes_projection_load_stopped_message() {
			var loadStopped = HandledMessages.OfType<CoreProjectionManagementMessage.LoadStopped>().LastOrDefault();
			Assert.IsNotNull(loadStopped);
			Assert.AreEqual(_projectionId, loadStopped.ProjectionId);
		}
	}
}
