using System;

namespace EventStore.Projections.Core.Services.Processing.WorkItems {
	class NotAuthorizedWorkItem : CheckpointWorkItemBase {
		public NotAuthorizedWorkItem()
			: base(null) {
		}

		protected override void ProcessEvent() {
			throw new Exception("Projection cannot read its source. Not authorized.");
		}
	}
}
