using System;
using EventStore.Common.Utils;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static partial class ReplicationTrackingMessage {
		[DerivedMessage(CoreMessage.ReplicationTracking)]
		public partial class WriterCheckpointFlushed : Message<WriterCheckpointFlushed> {
		}

		[DerivedMessage(CoreMessage.ReplicationTracking)]
		public partial class IndexedTo : Message<IndexedTo> {
			public readonly long LogPosition;
			
			public IndexedTo(long logPosition ) {
				Ensure.Nonnegative(logPosition + 1, "logPosition");
				LogPosition = logPosition;
			}
		}

		[DerivedMessage(CoreMessage.ReplicationTracking)]
		public partial class ReplicatedTo : Message<ReplicatedTo> {
			public readonly long LogPosition;
			
			public ReplicatedTo(long logPosition ) {
				Ensure.Nonnegative(logPosition + 1, "logPosition");
				LogPosition = logPosition;
			}
		}

		[DerivedMessage(CoreMessage.ReplicationTracking)]
		public partial class LeaderReplicatedTo : Message<LeaderReplicatedTo> {
			public readonly long LogPosition;
			
			public LeaderReplicatedTo(long logPosition ) {
				Ensure.Nonnegative(logPosition + 1, "logPosition");
				LogPosition = logPosition;
			}
		}

		[DerivedMessage(CoreMessage.ReplicationTracking)]
		public partial class ReplicaWriteAck : Message<ReplicaWriteAck> {
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
