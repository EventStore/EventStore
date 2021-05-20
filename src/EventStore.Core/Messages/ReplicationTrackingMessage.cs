using System;
using EventStore.Common.Utils;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static class ReplicationTrackingMessage {
		public class WriterCheckpointFlushed : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public class IndexedTo : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly long LogPosition;

			public IndexedTo(long logPosition) {
				Ensure.Nonnegative(logPosition + 1, "logPosition");
				LogPosition = logPosition;
			}
		}

		public class ReplicatedTo : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly long LogPosition;

			public ReplicatedTo(long logPosition) {
				Ensure.Nonnegative(logPosition + 1, "logPosition");
				LogPosition = logPosition;
			}
		}

		public class LeaderReplicatedTo : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly long LogPosition;

			public LeaderReplicatedTo(long logPosition) {
				Ensure.Nonnegative(logPosition + 1, "logPosition");
				LogPosition = logPosition;
			}
		}

		public class ReplicaWriteAck : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

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
