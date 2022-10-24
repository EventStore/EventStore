using System;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static partial class SubscriptionMessage {
		[StatsGroup("subscription")]
		public enum MessageType {
			None = 0,
			PollStream = 1,
			CheckPollTimeout = 2,
			PersistentSubscriptionTimerTick = 3,
			PersistentSubscriptionsRestart = 4,
			PersistentSubscriptionsRestarting = 5,
			InvalidPersistentSubscriptionsRestart = 6,
			PersistentSubscriptionsStarted = 7,
			PersistentSubscriptionsStopped = 8,
		}

		[StatsMessage(MessageType.PollStream)]
		public partial class PollStream : Message {
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

		[StatsMessage(MessageType.CheckPollTimeout)]
		public partial class CheckPollTimeout : Message {
		}

		[StatsMessage(MessageType.PersistentSubscriptionTimerTick)]
		public partial class PersistentSubscriptionTimerTick : Message {
			public Guid CorrelationId { get; }

			public PersistentSubscriptionTimerTick(Guid correlationId) {
				CorrelationId = correlationId;
			}
		}
		
		[StatsMessage(MessageType.PersistentSubscriptionsRestart)]
		public partial class PersistentSubscriptionsRestart : Message {
			public IEnvelope ReplyEnvelope { get; }
			
			public PersistentSubscriptionsRestart(IEnvelope replyEnvelope) {
				ReplyEnvelope = replyEnvelope;
			}
		}

		[StatsMessage(MessageType.PersistentSubscriptionsRestarting)]
		public partial class PersistentSubscriptionsRestarting : Message {
		}

		[StatsMessage(MessageType.InvalidPersistentSubscriptionsRestart)]
		public partial class InvalidPersistentSubscriptionsRestart : Message {
		}
	
		[StatsMessage(MessageType.PersistentSubscriptionsStarted)]
		public partial class PersistentSubscriptionsStarted : Message {
		}
		
		[StatsMessage(MessageType.PersistentSubscriptionsStopped)]
		public partial class PersistentSubscriptionsStopped : Message {
		}
	}
}
