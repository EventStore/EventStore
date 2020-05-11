using System;

namespace EventStore.ClientAPI {
	internal static class Consts {
		public const int DefaultMaxQueueSize = 5000;
		public const int DefaultMaxConcurrentItems = 5000;
		public const int DefaultMaxOperationRetries = 10;
		public const int DefaultMaxReconnections = 10;

		public const bool DefaultRequireLeader = true;

		public static readonly TimeSpan DefaultReconnectionDelay = TimeSpan.FromMilliseconds(100);
		public static readonly TimeSpan DefaultQueueTimeout = TimeSpan.Zero; // Unlimited
		public static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromSeconds(7);
		public static readonly TimeSpan DefaultOperationTimeoutCheckPeriod = TimeSpan.FromSeconds(1);

		public static readonly TimeSpan TimerPeriod = TimeSpan.FromMilliseconds(200);
		public static readonly int MaxReadSize = 4096;
		public const int DefaultMaxClusterDiscoverAttempts = 10;
		public const int DefaultHttpPort = 2113;

		public const int CatchUpDefaultReadBatchSize = 500;
		public const int CatchUpDefaultMaxPushQueueSize = 10000;
	}
}
