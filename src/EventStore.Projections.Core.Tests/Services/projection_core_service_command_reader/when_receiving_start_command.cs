using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_command_reader {
	[TestFixture]
	public class when_receiving_start_command : specification_with_projection_core_service_command_reader_started {
		private Guid _projectionId;

		protected override IEnumerable<WhenStep> When() {
			_projectionId = Guid.NewGuid();
			yield return
				CreateWriteEvent(
					"$projections-$" + _serviceId,
					"$start",
					"{\"id\":\"" + _projectionId.ToString("N") + "\"}",
					null,
					true);
		}

		[Test]
		public void publishes_projection_start_message() {
			var start = HandledMessages.OfType<CoreProjectionManagementMessage.Start>().LastOrDefault();
			Assert.IsNotNull(start);
			Assert.AreEqual(_projectionId, start.ProjectionId);
		}
	}
}
