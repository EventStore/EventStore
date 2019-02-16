using EventStore.ClientAPI.ClientOperations;

namespace EventStore.ClientAPI.Internal {
	internal class VolatileEventStoreSubscription : EventStoreSubscription {
		private readonly VolatileSubscriptionOperation _subscriptionOperation;

		internal VolatileEventStoreSubscription(VolatileSubscriptionOperation subscriptionOperation, string streamId,
			long lastCommitPosition, long? lastEventNumber)
			: base(streamId, lastCommitPosition, lastEventNumber) {
			_subscriptionOperation = subscriptionOperation;
		}

		public override void Unsubscribe() {
			_subscriptionOperation.Unsubscribe();
		}
	}
}
