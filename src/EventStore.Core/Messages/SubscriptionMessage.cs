using System;
using EventStore.Core.Messaging;
using EventStore.Core.Services.PersistentSubscription;

namespace EventStore.Core.Messages {
	public static class SubscriptionMessage {
		public class PollStream : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string StreamId;
			public readonly long LastIndexedPosition;
			public readonly long? LastEventNumber;
			public readonly DateTime ExpireAt;

			public readonly Message OriginalRequest;

			public PollStream(string streamId, long lastIndexedPosition, long? lastEventNumber, DateTime expireAt,
				Message originalRequest) {
				StreamId = streamId;
				LastIndexedPosition = lastIndexedPosition;
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

			public Guid CorrelationId { get; }

			public PersistentSubscriptionTimerTick(Guid correlationId) {
				CorrelationId = correlationId;
			}
		}

		public class PersistentSubscriptionsRestart : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public IEnvelope ReplyEnvelope { get; }

			public PersistentSubscriptionsRestart(IEnvelope replyEnvelope) {
				ReplyEnvelope = replyEnvelope;
			}
		}

		public class PersistentSubscriptionsRestarting : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public class InvalidPersistentSubscriptionsRestart : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public class PersistentSubscriptionsStarted : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public class PersistentSubscriptionsStopped : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}
	}
}
