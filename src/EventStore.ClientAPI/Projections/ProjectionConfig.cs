using System;

namespace EventStore.ClientAPI.Projections {
	/// <summary>
	/// Provides the configuration for a projection.
	/// </summary>
	public sealed class ProjectionConfig {
		/// <summary>
		/// Whether this projection can emit events using emit() or linkTo().
		/// </summary>
		public readonly bool EmitEnabled = false;
		/// <summary>
		/// Whether this projection tracks emitted streams. This enables you to delete a projection and all the streams that it has created.
		/// </summary>
		public readonly bool TrackEmittedStreams = false;
		/// <summary>
		/// Minimum time (in ms) after which to write a projection checkpoint.
		/// </summary>
		public readonly int CheckpointAfterMs = 0;
		/// <summary>
		/// Number of events that a projection can handle before attempting to write a checkpoint.
		/// </summary>
		public readonly int CheckpointHandledThreshold = 4000;
		/// <summary>
		/// Number of bytes a projection can process before attempting to write a checkpoint.
		/// </summary>
		public readonly int CheckpointUnhandledBytesThreshold = 10*1000*1000;
		/// <summary>
		/// Number of events that can be pending before the projection is temporarily paused.
		/// </summary>
		public readonly int PendingEventsThreshold = 5000;
		/// <summary>
		/// Maximum number of events the projection can write in a batch at a time.
		/// </summary>
		public readonly int MaxWriteBatchLength = 500;
		/// <summary>
		/// Maximum number of concurrent writes to allow for a projection.
		/// </summary>
		public readonly int MaxAllowedWritesInFlight = 0;

		/// <summary>
		/// create a new <see cref="ProjectionConfig"/> class.
		/// </summary>
		/// <param name="emitEnabled">Whether this projection can emit events using emit() or linkTo(). Default: false</param>
		/// <param name="trackEmittedStreams">Whether this projection tracks emitted streams. Default: false</param>
		/// <param name="checkpointAfterMs">Minimum time (in ms) after which to write a projection checkpoint. Default: 0 (disabled)</param>
		/// <param name="checkpointHandledThreshold">Number of events that a projection can handle before attempting to write a checkpoint. Default: 4000</param>
		/// <param name="checkpointUnhandledBytesThreshold">Number of bytes a projection can process before attempting to write a checkpoint. Default: 10000000 (10 MB)</param>
		/// <param name="pendingEventsThreshold">Number of events that can be pending before the projection is temporarily paused. Default: 5000</param>
		/// <param name="maxWriteBatchLength">Maximum number of events the projection can write in a batch at a time. Default: 500</param>
		/// <param name="maxAllowedWritesInFlight">Maximum number of concurrent writes to allow for a projection. Default: 0 (Unbounded)</param>
		public ProjectionConfig(
			bool emitEnabled,
			bool trackEmittedStreams,
			int checkpointAfterMs,
			int checkpointHandledThreshold,
			int checkpointUnhandledBytesThreshold,
			int pendingEventsThreshold,
			int maxWriteBatchLength,
			int maxAllowedWritesInFlight
			) {
			EmitEnabled = emitEnabled;
			TrackEmittedStreams = trackEmittedStreams;
			CheckpointAfterMs = checkpointAfterMs;
			CheckpointHandledThreshold = checkpointHandledThreshold;
			CheckpointUnhandledBytesThreshold = checkpointUnhandledBytesThreshold;
			PendingEventsThreshold = pendingEventsThreshold;
			MaxWriteBatchLength = maxWriteBatchLength;
			MaxAllowedWritesInFlight = maxAllowedWritesInFlight;
		}
	}
}
