namespace EventStore.Client.Projections {
	/// <summary>
	/// Provides the details for a projection.
	/// </summary>
	public sealed class ProjectionDetails {
		/// <summary>
		/// The CoreProcessingTime
		/// </summary>
		public readonly long CoreProcessingTime;

		/// <summary>
		/// The projection version
		/// </summary>
		public readonly long Version;

		/// <summary>
		/// The Epoch
		/// </summary>
		public readonly long Epoch;

		/// <summary>
		/// The projection EffectiveName
		/// </summary>
		public readonly string EffectiveName;

		/// <summary>
		/// The projection WritesInProgress
		/// </summary>
		public readonly int WritesInProgress;

		/// <summary>
		/// The projection ReadsInProgress
		/// </summary>
		public readonly int ReadsInProgress;

		/// <summary>
		/// The projection PartitionsCached
		/// </summary>
		public readonly int PartitionsCached;

		/// <summary>
		/// The projection Status
		/// </summary>
		public readonly string Status;

		/// <summary>
		/// The projection StateReason
		/// </summary>
		public readonly string StateReason;

		/// <summary>
		/// The projection Name
		/// </summary>
		public readonly string Name;

		/// <summary>
		/// The projection Mode
		/// </summary>
		public readonly string Mode;

		/// <summary>
		/// The projection Position
		/// </summary>
		public readonly string Position;

		/// <summary>
		/// The projection Progress
		/// </summary>
		public readonly float Progress;

		/// <summary>
		/// LastCheckpoint
		/// </summary>
		public readonly string LastCheckpoint;

		/// <summary>
		/// The projection EventsProcessedAfterRestart
		/// </summary>
		public readonly long EventsProcessedAfterRestart;

		/// <summary>
		/// The projection CheckpointStatus
		/// </summary>
		public readonly string CheckpointStatus;

		/// <summary>
		/// The projection BufferedEvents
		/// </summary>
		public readonly long BufferedEvents;

		/// <summary>
		/// The projection WritePendingEventsBeforeCheckpoint
		/// </summary>
		public readonly int WritePendingEventsBeforeCheckpoint;

		/// <summary>
		/// The projection WritePendingEventsAfterCheckpoint
		/// </summary>
		public readonly int WritePendingEventsAfterCheckpoint;

		/// <summary>
		/// create a new <see cref="ProjectionDetails"/> class.
		/// </summary>
		/// <param name="coreProcessingTime"></param>
		/// <param name="version"></param>
		/// <param name="epoch"></param>
		/// <param name="effectiveName"></param>
		/// <param name="writesInProgress"></param>
		/// <param name="readsInProgress"></param>
		/// <param name="partitionsCached"></param>
		/// <param name="status"></param>
		/// <param name="stateReason"></param>
		/// <param name="name"></param>
		/// <param name="mode"></param>
		/// <param name="position"></param>
		/// <param name="progress"></param>
		/// <param name="lastCheckpoint"></param>
		/// <param name="eventsProcessedAfterRestart"></param>
		/// <param name="checkpointStatus"></param>
		/// <param name="bufferedEvents"></param>
		/// <param name="writePendingEventsBeforeCheckpoint"></param>
		/// <param name="writePendingEventsAfterCheckpoint"></param>
		public ProjectionDetails(
			long coreProcessingTime,
			long version,
			long epoch,
			string effectiveName,
			int writesInProgress,
			int readsInProgress,
			int partitionsCached,
			string status,
			string stateReason,
			string name,
			string mode,
			string position,
			float progress,
			string lastCheckpoint,
			long eventsProcessedAfterRestart,
			string checkpointStatus,
			long bufferedEvents,
			int writePendingEventsBeforeCheckpoint,
			int writePendingEventsAfterCheckpoint) {
			CoreProcessingTime = coreProcessingTime;
			Version = version;
			Epoch = epoch;
			EffectiveName = effectiveName;
			WritesInProgress = writesInProgress;
			ReadsInProgress = readsInProgress;
			PartitionsCached = partitionsCached;
			Status = status;
			StateReason = stateReason;
			Name = name;
			Mode = mode;
			Position = position;
			Progress = progress;
			LastCheckpoint = lastCheckpoint;
			EventsProcessedAfterRestart = eventsProcessedAfterRestart;
			CheckpointStatus = checkpointStatus;
			BufferedEvents = bufferedEvents;
			WritePendingEventsBeforeCheckpoint = writePendingEventsBeforeCheckpoint;
			WritePendingEventsAfterCheckpoint = writePendingEventsAfterCheckpoint;
		}
	}
}
