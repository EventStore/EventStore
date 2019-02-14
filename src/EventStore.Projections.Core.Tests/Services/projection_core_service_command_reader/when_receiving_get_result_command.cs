using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_command_reader {
	[TestFixture]
	public class when_receiving_get_result_command : specification_with_projection_core_service_command_reader_started {
		private Guid _projectionId;
		private Guid _correlationId;
		private string _partition;

		protected override IEnumerable<WhenStep> When() {
			_projectionId = Guid.NewGuid();
			_correlationId = Guid.NewGuid();
			_partition = "partition";
			yield return
				CreateWriteEvent(
					"$projections-$" + _serviceId,
					"$get-result",
					@"{
                        ""id"":""" + _projectionId.ToString("N") + @""",
                        ""correlationId"":""" + _correlationId.ToString("N") + @""",
                        ""partition"":""" + _partition + @""",
                    }",
					null,
					true);
		}

		[Test]
		public void publishes_projection_get_result_message() {
			var command = HandledMessages.OfType<CoreProjectionManagementMessage.GetResult>().LastOrDefault();
			Assert.IsNotNull(command);
			Assert.AreEqual(_projectionId, command.ProjectionId);
			Assert.AreEqual(_correlationId, command.CorrelationId);
			Assert.AreEqual(_partition, command.Partition);
		}
	}
}
