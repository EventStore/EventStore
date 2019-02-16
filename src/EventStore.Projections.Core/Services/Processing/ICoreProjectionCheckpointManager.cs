using System;

namespace EventStore.Projections.Core.Services.Processing {
	public interface ICoreProjectionCheckpointReader {
		void BeginLoadState();
		void Initialize();
	}

	public interface IEmittedEventWriter {
		void EventsEmitted(EmittedEventEnvelope[] scheduledWrites, Guid causedBy, string correlationId);
	}

	public interface ICoreProjectionCheckpointManager {
		void Initialize();
		void Start(CheckpointTag checkpointTag, PartitionState rootPartitionState);
		void Stopping();
		void Stopped();
		void GetStatistics(ProjectionStatistics info);


		void StateUpdated(string partition, PartitionState oldState, PartitionState newState);
		void PartitionCompleted(string partition);
		void EventProcessed(CheckpointTag checkpointTag, float progress);

		/// <summary>
		/// Suggests a checkpoint which may complete immediately or be delayed
		/// </summary>
		/// <param name="checkpointTag"></param>
		/// <param name="progress"></param>
		/// <returns>true - if checkpoint has been completed (or skipped)</returns>
		bool CheckpointSuggested(CheckpointTag checkpointTag, float progress);

		void Progress(float progress);

		void BeginLoadPrerecordedEvents(CheckpointTag checkpointTag);

		void BeginLoadPartitionStateAt(
			string statePartition, CheckpointTag requestedStateCheckpointTag,
			Action<PartitionState> loadCompleted);

		void RecordEventOrder(ResolvedEvent resolvedEvent, CheckpointTag orderCheckpointTag, Action committed);
		CheckpointTag LastProcessedEventPosition { get; }
	}
}
