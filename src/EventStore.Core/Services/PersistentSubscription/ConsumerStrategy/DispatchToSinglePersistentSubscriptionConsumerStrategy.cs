using EventStore.Core.Data;

namespace EventStore.Core.Services.PersistentSubscription.ConsumerStrategy {
	class DispatchToSinglePersistentSubscriptionConsumerStrategy : RoundRobinPersistentSubscriptionConsumerStrategy {
		public override string Name {
			get { return SystemConsumerStrategies.DispatchToSingle; }
		}

		public override ConsumerPushResult PushMessageToClient(ResolvedEvent ev, int retryCount) {
			for (int i = 0; i < Clients.Count; i++) {
				if (Clients.Peek().Push(ev, retryCount)) {
					return ConsumerPushResult.Sent;
				}

				var c = Clients.Dequeue();
				Clients.Enqueue(c);
			}

			return ConsumerPushResult.NoMoreCapacity;
		}
	}
}
