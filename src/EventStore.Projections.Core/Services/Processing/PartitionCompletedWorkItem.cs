using System;

namespace EventStore.Projections.Core.Services.Processing {
	class PartitionCompletedWorkItem : CheckpointWorkItemBase {
		private readonly IEventProcessingProjectionPhase _projection;
		private readonly ICoreProjectionCheckpointManager _checkpointManager;
		private readonly string _partition;
		private readonly CheckpointTag _checkpointTag;
		private PartitionState _state;

		public PartitionCompletedWorkItem(
			IEventProcessingProjectionPhase projection, ICoreProjectionCheckpointManager checkpointManager,
			string partition, CheckpointTag checkpointTag)
			: base() {
			_projection = projection;
			_checkpointManager = checkpointManager;
			_partition = partition;
			_checkpointTag = checkpointTag;
		}

		protected override void Load(CheckpointTag checkpointTag) {
			if (_partition == null)
				throw new NotSupportedException();
			_projection.BeginGetPartitionStateAt(_partition, _checkpointTag, LoadCompleted, lockLoaded: false);
		}

		private void LoadCompleted(PartitionState obj) {
			_state = obj;
			NextStage();
		}

		protected override void WriteOutput() {
			_projection.EmitEofResult(_partition, _state.Result, _checkpointTag, Guid.Empty, null);
			// NOTE: write output is an ordered processing stage
			//      thus all the work items before have been already processed
			//      and as we are processing in the stream-by-stream mode
			//      it is safe to clean everything before this position up
			_projection.UnlockAndForgetBefore(_checkpointTag);
			_checkpointManager.PartitionCompleted(_partition);
			NextStage();
		}
	}
}
