using System;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Builder used for creating instances of ClusterSettings.
    /// </summary>
    public class ClusterSettingsBuilder
    {
        /// <summary>
        /// Sets the client to discover nodes using a DNS name and a well-known port.
        /// </summary>
        /// <returns>A <see cref="DnsClusterSettingsBuilder"/> for further configuration.</returns>
        [Obsolete("This class will be obsoleted, use ConnectionSettings and/or connection strings instead.")]
        public DnsClusterSettingsBuilder DiscoverClusterViaDns()
        {
            return new DnsClusterSettingsBuilder();
        }

        /// <summary>
        /// Sets the client to discover cluster nodes by specifying the IP endpoints of
        /// one or more of the nodes.
        /// </summary>
        /// <returns></returns>
        [Obsolete("This class will be obsoleted, use ConnectionSettings and/or connection strings instead.")]
        public GossipSeedClusterSettingsBuilder DiscoverClusterViaGossipSeeds()
        {
            return new GossipSeedClusterSettingsBuilder();
        }
    }
}