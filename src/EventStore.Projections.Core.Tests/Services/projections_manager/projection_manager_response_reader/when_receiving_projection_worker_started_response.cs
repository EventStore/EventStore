using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.projection_manager_response_reader {
	[TestFixture]
	public class when_receiving_projection_worker_started_response
		: specification_with_projection_manager_response_reader_started {
		private Guid _workerId;

		protected override IEnumerable<WhenStep> When() {
			_workerId = Guid.NewGuid();
			yield return CreateWriteEvent("$projections-$master", "$projection-worker-started", @"{
                        ""id"":""" + _workerId.ToString("N") + @""",
                    }", null, true);
		}

		[Test]
		public void publishes_projection_worker_started_message() {
			var response = HandledMessages.OfType<CoreProjectionStatusMessage.ProjectionWorkerStarted>()
				.LastOrDefault();
			Assert.IsNotNull(response);
			Assert.AreEqual(_workerId, response.WorkerId);
		}
	}
}
