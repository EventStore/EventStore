using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing {
	public class CheckpointSuggestedWorkItem : CheckpointWorkItemBase {
		private readonly IProjectionPhaseCheckpointManager _projectionPhase;
		private readonly EventReaderSubscriptionMessage.CheckpointSuggested _message;
		private readonly ICoreProjectionCheckpointManager _checkpointManager;

		private bool _completed = false;
		private bool _completeRequested = false;

		public CheckpointSuggestedWorkItem(
			IProjectionPhaseCheckpointManager projectionPhase,
			EventReaderSubscriptionMessage.CheckpointSuggested message,
			ICoreProjectionCheckpointManager checkpointManager)
			: base() {
			_projectionPhase = projectionPhase;
			_message = message;
			_checkpointManager = checkpointManager;
		}

		protected override void WriteOutput() {
			_projectionPhase.SetCurrentCheckpointSuggestedWorkItem(this);
			if (_checkpointManager.CheckpointSuggested(_message.CheckpointTag, _message.Progress)) {
				_projectionPhase.SetCurrentCheckpointSuggestedWorkItem(null);
				_completed = true;
			}

			_projectionPhase.NewCheckpointStarted(_message.CheckpointTag);
			NextStage();
		}

		protected override void CompleteItem() {
			if (_completed)
				NextStage();
			else
				_completeRequested = true;
		}

		internal void CheckpointCompleted() {
			if (_completeRequested)
				NextStage();
			else
				_completed = true;
		}
	}
}
