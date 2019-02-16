using System;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static class SubscriptionMessage {
		public class PollStream : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string StreamId;
			public readonly long LastCommitPosition;
			public readonly long? LastEventNumber;
			public readonly DateTime ExpireAt;

			public readonly Message OriginalRequest;

			public PollStream(string streamId, long lastCommitPosition, long? lastEventNumber, DateTime expireAt,
				Message originalRequest) {
				StreamId = streamId;
				LastCommitPosition = lastCommitPosition;
				LastEventNumber = lastEventNumber;
				ExpireAt = expireAt;
				OriginalRequest = originalRequest;
			}
		}

		public class CheckPollTimeout : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public class PersistentSubscriptionTimerTick : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}
	}
}
