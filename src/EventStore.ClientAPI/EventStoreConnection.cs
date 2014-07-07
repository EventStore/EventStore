using System.Net;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Core;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Contains factory methods for building connections to an Event Store server.
    /// </summary>
    public static class EventStoreConnection
    {
        /// <summary>
        /// Creates a new <see cref="IEventStoreConnection"/> to single node using default <see cref="ConnectionSettings"/>
        /// </summary>
        /// <param name="connectionName">Optional name of connection (will be generated automatically, if not provided)</param>
        /// <param name="tcpEndPoint">The <see cref="IPEndPoint"/> to connect to.</param>
        /// <returns>a new <see cref="IEventStoreConnection"/></returns>
        public static IEventStoreConnection Create(IPEndPoint tcpEndPoint, string connectionName = null)
        {
            return Create(ConnectionSettings.Default, tcpEndPoint, connectionName);
        }

        /// <summary>
        /// Creates a new <see cref="IEventStoreConnection"/> to single node using specific <see cref="ConnectionSettings"/>
        /// </summary>
        /// <param name="settings">The <see cref="ConnectionSettings"/> to apply to the new connection</param>
        /// <param name="tcpEndPoint">The <see cref="IPEndPoint"/> to connect to.</param>
        /// <param name="connectionName">Optional name of connection (will be generated automatically, if not provided)</param>
        /// <returns>a new <see cref="IEventStoreConnection"/></returns>
        public static IEventStoreConnection Create(ConnectionSettings settings, IPEndPoint tcpEndPoint, string connectionName = null)
        {
            Ensure.NotNull(settings, "settings");
            Ensure.NotNull(tcpEndPoint, "tcpEndPoint");
            return new EventStoreNodeConnection(settings, new StaticEndPointDiscoverer(tcpEndPoint, settings.UseSslConnection), connectionName);
        }
        
        /// <summary>
        /// Creates a new <see cref="IEventStoreConnection"/> to EventStore cluster 
        /// using specific <see cref="ConnectionSettings"/> and <see cref="ClusterSettings"/>
        /// </summary>
        /// <param name="connectionSettings">The <see cref="ConnectionSettings"/> to apply to the new connection</param>
        /// <param name="clusterSettings">The <see cref="ClusterSettings"/> that determine cluster behavior.</param>
        /// <param name="connectionName">Optional name of connection (will be generated automatically, if not provided)</param>
        /// <returns>a new <see cref="IEventStoreConnection"/></returns>
        public static IEventStoreConnection Create(ConnectionSettings connectionSettings, ClusterSettings clusterSettings, string connectionName = null)
        {
            Ensure.NotNull(connectionSettings, "connectionSettings");
            Ensure.NotNull(clusterSettings, "clusterSettings");

            var endPointDiscoverer = new ClusterDnsEndPointDiscoverer(connectionSettings.Log,
                                                                      clusterSettings.ClusterDns,
                                                                      clusterSettings.MaxDiscoverAttempts,
                                                                      clusterSettings.ExternalGossipPort,
                                                                      clusterSettings.GossipSeeds,
                                                                      clusterSettings.GossipTimeout);

            return new EventStoreNodeConnection(connectionSettings, endPointDiscoverer, connectionName);
        }
    }
}