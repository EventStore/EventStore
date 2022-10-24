using EventStore.Common.Utils;
using EventStore.Core.Cluster;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static partial class LeaderDiscoveryMessage {
		[StatsGroup("leader-discovery")]
		public enum MessageType {
			None = 0,
			LeaderFound = 1,
			DiscoveryTimeout = 2,
		}

		[StatsMessage(MessageType.LeaderFound)]
		public partial class LeaderFound : Message {
			public readonly MemberInfo Leader;

			public LeaderFound(MemberInfo leader) {
				Ensure.NotNull(leader, "leader");
				Leader = leader;
			}
		}

		[StatsMessage(MessageType.DiscoveryTimeout)]
		public partial class DiscoveryTimeout : Message {
		}
	}
}
