using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Cluster;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static class LeaderDiscoveryMessage {
		public class LeaderFound : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly MemberInfo Leader;

			public LeaderFound(MemberInfo leader) {
				Ensure.NotNull(leader, "leader");
				Leader = leader;
			}
		}

		public class DiscoveryTimeout : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}
	}
}
