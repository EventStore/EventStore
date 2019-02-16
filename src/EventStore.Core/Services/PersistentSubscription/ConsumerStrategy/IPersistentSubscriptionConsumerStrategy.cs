using EventStore.Core.Data;

namespace EventStore.Core.Services.PersistentSubscription.ConsumerStrategy {
	public interface IPersistentSubscriptionConsumerStrategy {
		string Name { get; }

		void ClientAdded(PersistentSubscriptionClient client);

		void ClientRemoved(PersistentSubscriptionClient client);

		ConsumerPushResult PushMessageToClient(ResolvedEvent ev, int retryCount);
	}
}
