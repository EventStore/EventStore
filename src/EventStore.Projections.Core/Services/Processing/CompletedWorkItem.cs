namespace EventStore.Projections.Core.Services.Processing {
	class CompletedWorkItem : CheckpointWorkItemBase {
		private readonly IProjectionPhaseCompleter _projection;

		public CompletedWorkItem(IProjectionPhaseCompleter projection)
			: base() {
			_projection = projection;
		}

		protected override void WriteOutput() {
			_projection.Complete();
			NextStage();
		}
	}
}
