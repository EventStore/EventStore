using System;

namespace EventStore.ClientAPI {
	internal class PersistentEventStoreSubscription : EventStoreSubscription {
		private readonly IConnectToPersistentSubscriptions _subscriptionOperation;

		internal PersistentEventStoreSubscription(IConnectToPersistentSubscriptions subscriptionOperation,
			string streamId, long lastCommitPosition, long? lastEventNumber)
			: base(streamId, lastCommitPosition, lastEventNumber) {
			_subscriptionOperation = subscriptionOperation;
		}

		public override void Unsubscribe() {
			_subscriptionOperation.Unsubscribe();
		}

		public void NotifyEventsProcessed(Guid[] processedEvents) {
			_subscriptionOperation.NotifyEventsProcessed(processedEvents);
		}

		public void NotifyEventsFailed(Guid[] processedEvents, PersistentSubscriptionNakEventAction action,
			string reason) {
			_subscriptionOperation.NotifyEventsFailed(processedEvents, action, reason);
		}
	}
}
