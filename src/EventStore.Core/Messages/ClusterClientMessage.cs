using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static partial class ClusterClientMessage {
		[StatsGroup("cluster-client")]
		public enum MessageType {
			None = 0,
			CleanCache = 1,
		}

		[StatsMessage(MessageType.CleanCache)]
		public partial class CleanCache : Message {
			public CleanCache() { }
		}
	}
}
