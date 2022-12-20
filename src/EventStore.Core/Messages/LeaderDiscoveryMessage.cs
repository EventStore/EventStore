using EventStore.Common.Utils;
using EventStore.Core.Cluster;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static partial class LeaderDiscoveryMessage {
		[DerivedMessage]
		public partial class LeaderFound : Message {
			public readonly MemberInfo Leader;

			public LeaderFound(MemberInfo leader) {
				Ensure.NotNull(leader, "leader");
				Leader = leader;
			}
		}

		[DerivedMessage]
		public partial class DiscoveryTimeout : Message {
		}
	}
}
