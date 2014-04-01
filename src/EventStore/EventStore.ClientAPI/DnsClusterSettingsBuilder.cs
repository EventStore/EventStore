using System;
using System.Net;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Fluent builder used to configure <see cref="ClusterSettings" /> for connecting to a cluster
    /// using DNS discovery.
    /// </summary>
    public class DnsClusterSettingsBuilder
    {
        private string _clusterDns;
        private int _maxDiscoverAttempts = Consts.DefaultMaxClusterDiscoverAttempts;
        private int _managerExternalHttpPort = Consts.DefaultClusterManagerExternalHttpPort;
        private TimeSpan _gossipTimeout = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Sets the DNS name under which cluster nodes are listed.
        /// </summary>
        /// <param name="clusterDns">The DNS name under which cluster nodes are listed.</param>
        /// <returns>A <see cref="DnsClusterSettingsBuilder"/> for further configuration.</returns>
        /// <exception cref="ArgumentNullException">If <see cref="clusterDns" /> is null or empty.</exception>
        public DnsClusterSettingsBuilder SetClusterDns(string clusterDns)
        {
            Ensure.NotNullOrEmpty(clusterDns, "clusterDns");
            _clusterDns = clusterDns;
            return this;
        }

        /// <summary>
        /// Sets the maximum number of attempts for discovery.
        /// </summary>
        /// <param name="maxDiscoverAttempts">The maximum number of attempts for DNS discovery.</param>
        /// <returns>A <see cref="DnsClusterSettingsBuilder"/> for further configuration.</returns>
        /// <exception cref="ArgumentOutOfRangeException">If <see cref="maxDiscoverAttempts" /> is less than or equal to 0.</exception>
        public DnsClusterSettingsBuilder SetMaxDiscoverAttempts(int maxDiscoverAttempts)
        {
            if (maxDiscoverAttempts <= 0)
                throw new ArgumentOutOfRangeException("maxDiscoverAttempts", string.Format("maxDiscoverAttempts value is out of range: {0}. Allowed range: [-1, infinity].", maxDiscoverAttempts));
            _maxDiscoverAttempts = maxDiscoverAttempts;
            return this;
        }

        /// <summary>
        /// Sets the period after which gossip times out if none is received.
        /// </summary>
        /// <param name="timeout">The period after which gossip times out if none is received.</param>
        /// <returns>A <see cref="DnsClusterSettingsBuilder"/> for further configuration.</returns>
        public DnsClusterSettingsBuilder SetGossipTimeout(TimeSpan timeout)
        {
            _gossipTimeout = timeout;
            return this;
        }

        /// <summary>
        /// Sets the well-known port on which the cluster gossip is taking place.
        /// 
        /// If you are using the commercial edition of Event Store HA, with Manager nodes in
        /// place, this should be the port number of the External HTTP port on which the
        /// managers are running.
        /// 
        /// If you are using the open source edition of Event Store HA, this should be the
        /// External HTTP port that the nodes are running on. If you cannot use a well-known
        /// port for this across all nodes, you can instead use gossip seed discovery and set
        /// the <see cref="IPEndPoint" /> of some seed nodes instead.
        /// </summary>
        /// <param name="clusterGossipPort">The cluster gossip port.</param>
        /// <returns>A <see cref="DnsClusterSettingsBuilder"/> for further configuration.</returns>
        public DnsClusterSettingsBuilder SetClusterGossipPort(int clusterGossipPort)
        {
            Ensure.Positive(clusterGossipPort, "clusterGossipPort");
            _managerExternalHttpPort = clusterGossipPort;
            return this;
        }

        /// <summary>
        /// Builds a <see cref="ClusterSettings"/> object from a <see cref="DnsClusterSettingsBuilder"/>.
        /// </summary>
        /// <param name="builder"><see cref="DnsClusterSettingsBuilder"/> from which to build a <see cref="ClusterSettings"/></param>
        /// <returns></returns>
        public static implicit operator ClusterSettings(DnsClusterSettingsBuilder builder)
        {
            return new ClusterSettings(builder._clusterDns,
                builder._maxDiscoverAttempts,
                builder._managerExternalHttpPort,
                builder._gossipTimeout);
        }
    }
}