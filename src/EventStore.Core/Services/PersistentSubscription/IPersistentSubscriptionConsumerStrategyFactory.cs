using System;
using EventStore.Core.Bus;

namespace EventStore.Core.Services.PersistentSubscription
{
    public interface IPersistentSubscriptionConsumerStrategyFactory
    {
        string StrategyName { get; }

        IPersistentSubscriptionConsumerStrategy Create(string subscriptionId, IPublisher mainQueue, ISubscriber mainBus);
    }

    class DelegatePersistentSubscriptionConsumerStrategyFactory : IPersistentSubscriptionConsumerStrategyFactory
    {
        public string StrategyName { get; }

        private readonly Func<string, IPublisher, ISubscriber, IPersistentSubscriptionConsumerStrategy> _factory;

        public DelegatePersistentSubscriptionConsumerStrategyFactory(string strategyName, Func<string, IPublisher, ISubscriber, IPersistentSubscriptionConsumerStrategy> factory)
        {
            _factory = factory;
            StrategyName = strategyName;
        }

        public IPersistentSubscriptionConsumerStrategy Create(string subscriptionId, IPublisher mainQueue, ISubscriber mainBus)
        {
            return _factory(subscriptionId, mainQueue, mainBus);
        }
    }
}