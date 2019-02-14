using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_command_reader {
	[TestFixture]
	public class when_receiving_two_commands : specification_with_projection_core_service_command_reader_started {
		private Guid _projectionId;
		private Guid _projectionId2;

		protected override IEnumerable<WhenStep> When() {
			_projectionId = Guid.NewGuid();
			_projectionId2 = Guid.NewGuid();
			yield return
				CreateWriteEvent(
					"$projections-$" + _serviceId,
					"$start",
					"{\"id\":\"" + _projectionId.ToString("N") + "\"}",
					null,
					true);
			yield return
				CreateWriteEvent(
					"$projections-$" + _serviceId,
					"$start",
					"{\"id\":\"" + _projectionId2.ToString("N") + "\"}",
					null,
					true);
		}

		[Test]
		public void publishes_projection_start_message() {
			var startProjectionCommands = HandledMessages.OfType<CoreProjectionManagementMessage.Start>().ToArray();
			Assert.AreEqual(2, startProjectionCommands.Length);
		}
	}
}
