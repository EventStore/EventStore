using System;
using EventStore.Common.Utils;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static partial class ReplicationTrackingMessage {
		[StatsGroup("replication-tracking")]
		public enum MessageType {
			None = 0,
			WriterCheckpointFlushed = 1,
			IndexedTo = 2,
			ReplicatedTo = 3,
			LeaderReplicatedTo = 4,
			ReplicaWriteAck = 5,
		}

		[StatsMessage(MessageType.WriterCheckpointFlushed)]
		public partial class WriterCheckpointFlushed : Message {
		}

		[StatsMessage(MessageType.IndexedTo)]
		public partial class IndexedTo : Message {
			public readonly long LogPosition;
			
			public IndexedTo(long logPosition ) {
				Ensure.Nonnegative(logPosition + 1, "logPosition");
				LogPosition = logPosition;
			}
		}

		[StatsMessage(MessageType.ReplicatedTo)]
		public partial class ReplicatedTo : Message {
			public readonly long LogPosition;
			
			public ReplicatedTo(long logPosition ) {
				Ensure.Nonnegative(logPosition + 1, "logPosition");
				LogPosition = logPosition;
			}
		}

		[StatsMessage(MessageType.LeaderReplicatedTo)]
		public partial class LeaderReplicatedTo : Message {
			public readonly long LogPosition;
			
			public LeaderReplicatedTo(long logPosition ) {
				Ensure.Nonnegative(logPosition + 1, "logPosition");
				LogPosition = logPosition;
			}
		}

		[StatsMessage(MessageType.ReplicaWriteAck)]
		public partial class ReplicaWriteAck : Message {
			public readonly Guid SubscriptionId;
			public readonly long ReplicationLogPosition;

			public ReplicaWriteAck(Guid subscriptionId, long replicationLogPosition) {
				Ensure.NotEmptyGuid(subscriptionId, "subscriptionId");
				SubscriptionId = subscriptionId;
				ReplicationLogPosition = replicationLogPosition;
			}
		}
	}
}
