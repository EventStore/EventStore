using EventStore.Core.Services.PersistentSubscription;

namespace EventStore.Core.PluginModel
{
    public interface IPersistentSubscriptionConsumerStrategyPlugin
    {
        string Name { get; }

        string Version { get; }

        IPersistentSubscriptionConsumerStrategyFactory GetConsumerStrategyFactory();
    }
}
