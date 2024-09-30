// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing.Checkpointing;
using EventStore.Projections.Core.Services.Processing.Partitioning;
using EventStore.Projections.Core.Services.Processing.WorkItems;

namespace EventStore.Projections.Core.Services.Processing.Phases {
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
