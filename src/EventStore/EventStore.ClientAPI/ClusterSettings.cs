using System;
using System.Net;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Contains settings relating to a connection to a cluster. 
    /// </summary>
    public sealed class ClusterSettings
    {
        /// <summary>
        /// Creates a new set of <see cref="ClusterSettings"/>
        /// </summary>
        /// <returns>A <see cref="ClusterSettingsBuilder"/> that can be used to build up a <see cref="ClusterSettings"/></returns>
        public static ClusterSettingsBuilder Create()
        {
            return new ClusterSettingsBuilder();
        }

        /// <summary>
        /// The DNS name to use for discovering endpoints.
        /// </summary>
        public readonly string ClusterDns;
        /// <summary>
        /// The maximum number of attempts for discovering endpoints.
        /// </summary>
        public readonly int MaxDiscoverAttempts;
        /// <summary>
        /// The well-known endpoint on which cluster managers are running.
        /// </summary>
        public readonly int ExternalGossipPort;

        /// <summary>
        /// Endpoints for seeding gossip if not using DNS.
        /// </summary>
        public readonly IPEndPoint[] GossipSeeds;
        /// <summary>
        /// Timeout for cluster gossip.
        /// </summary>
        public TimeSpan GossipTimeout;

        internal ClusterSettings(string clusterDns, int maxDiscoverAttempts, int externalGossipPort, IPEndPoint[] gossipSeeds, TimeSpan gossipTimeout)
        {
            Ensure.NotNullOrEmpty(clusterDns, "clusterDns");
            if (maxDiscoverAttempts < -1)
                throw new ArgumentOutOfRangeException("maxDiscoverAttempts", string.Format("maxDiscoverAttempts value is out of range: {0}. Allowed range: [-1, infinity].", maxDiscoverAttempts));
            Ensure.Positive(externalGossipPort, "externalGossipPort");

            ClusterDns = clusterDns;
            MaxDiscoverAttempts = maxDiscoverAttempts;
            ExternalGossipPort = externalGossipPort;
            GossipTimeout = gossipTimeout;
            GossipSeeds = gossipSeeds;
        }
    }
}
