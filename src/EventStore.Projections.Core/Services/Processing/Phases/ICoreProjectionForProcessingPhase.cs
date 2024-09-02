using System;
using EventStore.Projections.Core.Services.Processing.Checkpointing;
using EventStore.Projections.Core.Services.Processing.WorkItems;

namespace EventStore.Projections.Core.Services.Processing.Phases {
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
