namespace EventStore.Projections.Core.Services.Processing {
	class ProgressWorkItem : CheckpointWorkItemBase {
		private readonly ICoreProjectionCheckpointManager _checkpointManager;
		private readonly IProgressResultWriter _resultWriter;
		private readonly float _progress;

		public ProgressWorkItem(ICoreProjectionCheckpointManager checkpointManager, IProgressResultWriter resultWriter,
			float progress)
			: base(null) {
			_checkpointManager = checkpointManager;
			_resultWriter = resultWriter;
			_progress = progress;
		}

		protected override void WriteOutput() {
			_checkpointManager.Progress(_progress);
			_resultWriter.WriteProgress(_progress);
			NextStage();
		}
	}
}
