using System;
using EventStore.Core.Messaging;
using EventStore.Core.Services;

namespace EventStore.Core.Messages {
	public static partial class SubscriptionMessage {
		[DerivedMessage(CoreMessage.Subscription)]
		public partial class PollStream : Message<PollStream> {
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

		[DerivedMessage(CoreMessage.Subscription)]
		public partial class CheckPollTimeout : Message<CheckPollTimeout> {
		}

		[DerivedMessage(CoreMessage.Subscription)]
		public partial class DropSubscription : Message<DropSubscription> {
			public readonly Guid SubscriptionId;
			public readonly SubscriptionDropReason DropReason;

			public DropSubscription(Guid subscriptionId, SubscriptionDropReason dropReason) {
				SubscriptionId = subscriptionId;
				DropReason = dropReason;
			}
		}

		[DerivedMessage(CoreMessage.Subscription)]
		public partial class PersistentSubscriptionTimerTick : Message<PersistentSubscriptionTimerTick> {
			public Guid CorrelationId { get; }

			public PersistentSubscriptionTimerTick(Guid correlationId) {
				CorrelationId = correlationId;
			}
		}
		
		[DerivedMessage(CoreMessage.Subscription)]
		public partial class PersistentSubscriptionsRestart : Message<PersistentSubscriptionsRestart> {
			public IEnvelope ReplyEnvelope { get; }
			
			public PersistentSubscriptionsRestart(IEnvelope replyEnvelope) {
				ReplyEnvelope = replyEnvelope;
			}
		}

		[DerivedMessage(CoreMessage.Subscription)]
		public partial class PersistentSubscriptionsRestarting : Message<PersistentSubscriptionsRestarting> {
		}

		[DerivedMessage(CoreMessage.Subscription)]
		public partial class InvalidPersistentSubscriptionsRestart : Message<InvalidPersistentSubscriptionsRestart> {
			public readonly string Reason;

			public InvalidPersistentSubscriptionsRestart(string reason) {
				Reason = reason;
			}
		}
	
		[DerivedMessage(CoreMessage.Subscription)]
		public partial class PersistentSubscriptionsStarted : Message<PersistentSubscriptionsStarted> {
		}
		
		[DerivedMessage(CoreMessage.Subscription)]
		public partial class PersistentSubscriptionsStopped : Message<PersistentSubscriptionsStopped> {
		}
	}
}
