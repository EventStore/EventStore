using EventStore.Core.Bus;

namespace EventStore.Core.Services.PersistentSubscription.ConsumerStrategy {
	public interface IPersistentSubscriptionConsumerStrategyFactory {
		string StrategyName { get; }

		IPersistentSubscriptionConsumerStrategy
			Create(string subscriptionId, IPublisher mainQueue, ISubscriber mainBus);
	}
}
