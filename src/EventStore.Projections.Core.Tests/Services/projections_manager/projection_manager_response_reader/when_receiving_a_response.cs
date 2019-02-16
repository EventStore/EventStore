using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.projection_manager_response_reader {
	[TestFixture]
	public class when_receiving_a_response : specification_with_projection_manager_response_reader_started {
		protected override IEnumerable<WhenStep> When() {
			yield return
				CreateWriteEvent(
					"$projections-$master",
					"$started",
					"{\"id\":\"" + Guid.NewGuid().ToString("N") + "\"}",
					null,
					true);
		}

		[Test]
		public void it_works() {
			Assert.Pass();
		}
	}
}
