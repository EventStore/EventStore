using System;

namespace EventStore.Projections.Core.Services.Processing {
	public interface ICoreProjectionForProcessingPhase {
		void CompletePhase();
		void SetFaulted(string reason);
		void SetFaulted(Exception ex);
		void SetFaulting(string reason);
		void SetCurrentCheckpointSuggestedWorkItem(CheckpointSuggestedWorkItem checkpointSuggestedWorkItem);
		void EnsureTickPending();
		CheckpointTag LastProcessedEventPosition { get; }
		void Subscribed();
	}
}
