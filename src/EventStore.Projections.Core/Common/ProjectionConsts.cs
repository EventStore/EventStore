using System;

namespace EventStore.Projections.Core.Common {
	public static class ProjectionConsts {
		public const int CheckpointHandledThreshold = 4000;
		public const int PendingEventsThreshold = 5000;
		public const int MaxWriteBatchLength = 500;
		public const int CheckpointUnhandledBytesThreshold = 10 * 1000 * 1000;
		public const int MaxAllowedWritesInFlight = AllowedWritesInFlight.Unbounded;
		public static TimeSpan CheckpointAfterMs = TimeSpan.FromSeconds(0);
	}
}
