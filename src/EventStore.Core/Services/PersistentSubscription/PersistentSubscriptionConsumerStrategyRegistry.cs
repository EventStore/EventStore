using System;
using System.Collections.Generic;

namespace EventStore.Core.Services.PersistentSubscription
{
    internal class PersistentSubscriptionConsumerStrategyRegistry
    {
        private readonly IDictionary<string, Func<IPersistentSubscriptionConsumerStrategy>> _factoryLookup = new Dictionary<string, Func<IPersistentSubscriptionConsumerStrategy>>()
        {
            {SystemConsumerStrategies.RoundRobin, () => new RoundRobinPersistentSubscriptionConsumerStrategy()},
            {SystemConsumerStrategies.DispatchToSingle, () => new DispatchToSinglePersistentSubscriptionConsumerStrategy()}
        };

        public IPersistentSubscriptionConsumerStrategy GetInstance(string namedConsumerStrategy)
        {
            if (!ValidateStrategy(namedConsumerStrategy))
            {
                throw new ArgumentException("The named consumer strategy is unknown.", "namedConsumerStrategy");
            }

            return _factoryLookup[namedConsumerStrategy]();
        }

        public bool ValidateStrategy(string name)
        {
            return _factoryLookup.ContainsKey(name);
        }
    }
}