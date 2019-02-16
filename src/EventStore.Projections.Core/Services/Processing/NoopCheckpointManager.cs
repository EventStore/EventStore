using System;
using EventStore.Core.Bus;

namespace EventStore.Projections.Core.Services.Processing {
	public sealed class NoopCheckpointManager : ICoreProjectionCheckpointManager {
		private readonly IPublisher _publisher;
		private readonly Guid _projectionCorrelationId;


		private CheckpointTag _lastCompletedCheckpointPosition;
		private readonly PositionTracker _lastProcessedEventPosition;
		private float _lastProcessedEventProgress;

		private int _eventsProcessedAfterRestart;
		private bool _started;
		private bool _stopping;
		private bool _stopped;

		public NoopCheckpointManager(
			IPublisher publisher, Guid projectionCorrelationId, ProjectionConfig projectionConfig, string name,
			PositionTagger positionTagger, ProjectionNamesBuilder namingBuilder) {
			if (publisher == null) throw new ArgumentNullException("publisher");
			if (projectionConfig == null) throw new ArgumentNullException("projectionConfig");
			if (name == null) throw new ArgumentNullException("name");
			if (positionTagger == null) throw new ArgumentNullException("positionTagger");
			if (namingBuilder == null) throw new ArgumentNullException("namingBuilder");
			if (name == "") throw new ArgumentException("name");

			_lastProcessedEventPosition = new PositionTracker(positionTagger);

			_publisher = publisher;
			_projectionCorrelationId = projectionCorrelationId;
		}


		public void RecordEventOrder(ResolvedEvent resolvedEvent, CheckpointTag orderCheckpointTag, Action committed) {
			committed();
		}

		public void BeginLoadPartitionStateAt(
			string statePartition, CheckpointTag requestedStateCheckpointTag, Action<PartitionState> loadCompleted) {
			throw new NotSupportedException();
		}

		public void Initialize() {
			_lastCompletedCheckpointPosition = null;
			_lastProcessedEventPosition.Initialize();
			_lastProcessedEventProgress = -1;

			_eventsProcessedAfterRestart = 0;
			_started = false;
			_stopping = false;
			_stopped = false;
		}

		public void Start(CheckpointTag checkpointTag, PartitionState rootPartitionState) {
			if (_started)
				throw new InvalidOperationException("Already started");
			_started = true;
			_lastProcessedEventPosition.UpdateByCheckpointTagInitial(checkpointTag);
			_lastProcessedEventProgress = -1;
			_lastCompletedCheckpointPosition = checkpointTag;
		}

		public void Stopping() {
			EnsureStarted();
			if (_stopping)
				throw new InvalidOperationException("Already stopping");
			_stopping = true;
			_publisher.Publish(
				new CoreProjectionProcessingMessage.CheckpointCompleted(
					_projectionCorrelationId, _lastCompletedCheckpointPosition));
		}

		public void Stopped() {
			EnsureStarted();
			_started = false;
			_stopped = true;
		}

		public void GetStatistics(ProjectionStatistics info) {
			info.Position = (_lastProcessedEventPosition.LastTag ?? (object)"").ToString();
			info.Progress = _lastProcessedEventProgress;
			info.LastCheckpoint = _lastCompletedCheckpointPosition != null
				? _lastCompletedCheckpointPosition.ToString()
				: "";
			info.EventsProcessedAfterRestart = _eventsProcessedAfterRestart;
			info.WritePendingEventsBeforeCheckpoint = 0;
			info.WritePendingEventsAfterCheckpoint = 0;
			info.ReadsInProgress = 0;
			info.WritesInProgress = 0;
			info.CheckpointStatus = "";
		}

		public void StateUpdated(string partition, PartitionState oldState, PartitionState newState) {
			if (_stopped)
				return;
			EnsureStarted();
			if (_stopping)
				throw new InvalidOperationException("Stopping");

			if (partition == "" && newState.State == null) // ignore non-root partitions and non-changed states
				throw new NotSupportedException("Internal check");
		}

		public void PartitionCompleted(string partition) {
		}

		public void EventProcessed(CheckpointTag checkpointTag, float progress) {
			if (_stopped)
				return;
			EnsureStarted();
			if (_stopping)
				throw new InvalidOperationException("Stopping");
			_eventsProcessedAfterRestart++;
			_lastProcessedEventPosition.UpdateByCheckpointTagForward(checkpointTag);
			_lastProcessedEventProgress = progress;
			// running state only
		}

		public bool CheckpointSuggested(CheckpointTag checkpointTag, float progress) {
			throw new InvalidOperationException("Checkpoints are not used");
		}

		public void Progress(float progress) {
			if (_stopping || _stopped)
				return;
			EnsureStarted();
			_lastProcessedEventProgress = progress;
		}

		public CheckpointTag LastProcessedEventPosition {
			get { return _lastProcessedEventPosition.LastTag; }
		}


		private void PrerecordedEventsLoaded(CheckpointTag checkpointTag) {
			_publisher.Publish(
				new CoreProjectionProcessingMessage.PrerecordedEventsLoaded(_projectionCorrelationId, checkpointTag));
		}

		private void EnsureStarted() {
			if (!_started)
				throw new InvalidOperationException("Not started");
		}

		public void BeginLoadPrerecordedEvents(CheckpointTag checkpointTag) {
			PrerecordedEventsLoaded(checkpointTag);
		}
	}
}
