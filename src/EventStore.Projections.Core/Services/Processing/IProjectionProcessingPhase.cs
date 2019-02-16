using System;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing {
	public enum PhaseState {
		Unknown,
		Stopped,
		Starting,
		Running,
	}


	public interface IProjectionProcessingPhase : IDisposable,
		IHandle<CoreProjectionManagementMessage.GetState>,
		IHandle<CoreProjectionManagementMessage.GetResult>,
		IHandle<CoreProjectionProcessingMessage.PrerecordedEventsLoaded> {
		CheckpointTag AdjustTag(CheckpointTag tag);

		void InitializeFromCheckpoint(CheckpointTag checkpointTag);

		//TODO: remove from - it is passed for validation purpose only
		void Subscribe(CheckpointTag from, bool fromCheckpoint);
		void AssignSlaves(SlaveProjectionCommunicationChannels slaveProjections);

		void ProcessEvent();

		void EnsureUnsubscribed();

		void SetProjectionState(PhaseState state);

		void GetStatistics(ProjectionStatistics info);

		CheckpointTag MakeZeroCheckpointTag();
		ICoreProjectionCheckpointManager CheckpointManager { get; }
		IEmittedStreamsTracker EmittedStreamsTracker { get; }
	}
}
