using System.Collections.Generic;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;
using System.Linq;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Core.Messages;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.projection_manager_response_reader {
	[TestFixture]
	public class when_starting : specification_with_projection_manager_response_reader {
		protected override IEnumerable<WhenStep> When() {
			yield return new ProjectionManagementMessage.Starting(System.Guid.NewGuid());
		}

		[Test]
		public void registers_core_service() {
			Assert.Pass();
		}
	}
}
