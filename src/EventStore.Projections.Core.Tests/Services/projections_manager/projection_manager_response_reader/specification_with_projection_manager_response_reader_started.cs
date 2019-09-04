using System.Collections.Generic;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.projection_manager_response_reader {
	public abstract class specification_with_projection_manager_response_reader_started
		: specification_with_projection_manager_response_reader {
		protected override IEnumerable<WhenStep> PreWhen() {
			yield return new ProjectionManagementMessage.Starting(System.Guid.NewGuid());
		}
	}
}
