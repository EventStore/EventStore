using EventStore.Core;
using EventStore.Core.Bus;

namespace EventStore.ClientAPI.Embedded
{
    public static class EmbeddedEventStoreConnection
    {
        private static IEventStoreConnection Create(IPublisher queue, ISubscriber bus, ConnectionSettings settings, string connectionName = null)
        {
            return new EventStoreEmbeddedNodeConnection(settings, connectionName, queue);
        }

        public static IEventStoreConnection Create(ClusterVNode eventStore, string connectionName = null)
        {
            return Create(eventStore, ConnectionSettings.Default, connectionName);
        }

        public static IEventStoreConnection Create(ClusterVNode eventStore, ConnectionSettings settings, string connectionName = null)
        {
            return Create(eventStore.MainQueue, eventStore.MainBus, settings, connectionName);
        }
    }
}