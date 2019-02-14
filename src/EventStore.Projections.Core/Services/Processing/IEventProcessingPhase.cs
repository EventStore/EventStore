using System;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing {
	public interface IProjectionPhaseCompleter {
		void Complete();
	}

	public interface IProjectionPhaseCheckpointManager {
		void NewCheckpointStarted(CheckpointTag checkpointTag);
		void SetCurrentCheckpointSuggestedWorkItem(CheckpointSuggestedWorkItem checkpointSuggestedWorkItem);
	}

	public interface IProjectionPhaseStateManager {
		void BeginGetPartitionStateAt(
			string statePartition, CheckpointTag at, Action<PartitionState> loadCompleted,
			bool lockLoaded);

		void UnlockAndForgetBefore(CheckpointTag checkpointTag);

		CheckpointTag LastProcessedEventPosition { get; }
	}

	public interface IEventProcessingProjectionPhase : IProjectionPhaseStateManager {
		string TransformCatalogEvent(EventReaderSubscriptionMessage.CommittedEventReceived message);

		EventProcessedResult ProcessCommittedEvent(EventReaderSubscriptionMessage.CommittedEventReceived message,
			string partition);

		void FinalizeEventProcessing(
			EventProcessedResult result, CheckpointTag eventCheckpointTag, float progress);

		void RecordEventOrder(ResolvedEvent resolvedEvent, CheckpointTag orderCheckpointTag, Action completed);

		void EmitEofResult(
			string partition, string resultBody, CheckpointTag causedBy, Guid causedByGuid, string correlationId);

		EventProcessedResult ProcessPartitionDeleted(string partition, CheckpointTag deletedPosition);
	}
}
