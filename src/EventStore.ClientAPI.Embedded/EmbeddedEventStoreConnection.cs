using EventStore.Core;
using EventStore.Core.Bus;

namespace EventStore.ClientAPI.Embedded
{
    public static class EmbeddedEventStoreConnection
    {
        public static IEventStoreConnection Create(IPublisher eventStoreQueue, string connectionName = null)
        {
            return Create(eventStoreQueue, ConnectionSettings.Default, connectionName);
        }

        public static IEventStoreConnection Create(IPublisher eventStoreQueue, ConnectionSettings settings, string connectionName = null)
        {
            return new EventStoreEmbeddedConnection(settings, connectionName, eventStoreQueue);
        }

        public static IEventStoreConnection Create(ClusterVNode eventStore, string connectionName = null)
        {
            return Create(eventStore, ConnectionSettings.Default, connectionName);
        }

        public static IEventStoreConnection Create(ClusterVNode eventStore, ConnectionSettings settings, string connectionName = null)
        {
            return Create(eventStore.MainQueue, settings, connectionName);
        }
    }
}