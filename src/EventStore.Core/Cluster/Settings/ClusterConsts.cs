using System;

namespace EventStore.Core.Cluster.Settings {
	public static class ClusterConsts {
		public const int SubscriptionLastEpochCount = 20;
		public static readonly TimeSpan TruncationSyncTimeout = TimeSpan.FromSeconds(60);
	}
}
