namespace EventStore.Projections.Core.Services.Processing {
	public class CheckpointWorkItemBase : WorkItem {
		private static readonly object _correlationId = new object();

		protected CheckpointWorkItemBase()
			: base(_correlationId) {
			_requiresRunning = true;
		}

		protected CheckpointWorkItemBase(object correlation)
			: base(correlation) {
		}
	}
}
