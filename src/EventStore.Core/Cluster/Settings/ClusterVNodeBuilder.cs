using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using EventStore.Core.Authentication;
using EventStore.Core.Services.Gossip;
using EventStore.Core.Services.Monitoring;

namespace EventStore.Core.Cluster.Settings
{
    public class ClusterVNodeBuilder
    {
        private IPEndPoint _internalTcp;
        private IPEndPoint _internalSecureTcp;
        private IPEndPoint _externalTcp;
        private IPEndPoint _externalSecureTcp;
        private IPEndPoint _internalHttp;
        private IPEndPoint _externalHttp;

        private string[] _httpPrefixes;
        private bool _enableTrustedAuth;
        private X509Certificate2 _certificate;
        private int _workerThreads;

        private bool _discoverViaDns;
        private string _clusterDns;
        private IPEndPoint[] _gossipSeeds;

        private TimeSpan _minFlushDelay;

        private int _clusterNodeCount;
        private int _prepareAckCount;
        private int _commitAckCount;
        private TimeSpan _prepareTimeout;
        private TimeSpan _commitTimeout;

        private int _nodePriority;

        private bool _useSsl;
        private string _sslTargetHost;
        private bool _sslValidateServer;

        private TimeSpan _statsPeriod;
        private StatsStorage _statsStorage;

        private IAuthenticationProviderFactory _authenticationProviderFactory;
        private bool _disableScavengeMerging;
        private bool _adminOnPublic;
        private bool _statsOnPublic;
        private bool _gossipOnPublic;
        private TimeSpan _gossipInterval;
        private TimeSpan _gossipAllowedTimeDifference;
        private TimeSpan _gossipTimeout;
        private TimeSpan _tcpTimeout;
        private bool _verifyDbHashes;
        private int _maxMemtableSize;
        private List<ISubsystem> _subsystems;
        private int _clusterGossipPort;


        private ClusterVNodeBuilder()
        {
            _clusterGossipPort = 2113;
            _subsystems = new List<ISubsystem>();
            _statsStorage = StatsStorage.None;
            _sslTargetHost = "";
            _sslValidateServer = false;
            _minFlushDelay = TimeSpan.FromMilliseconds(2);
            _gossipInterval = TimeSpan.FromSeconds(1);
            _gossipAllowedTimeDifference = TimeSpan.FromSeconds(30);
            _gossipTimeout = TimeSpan.FromSeconds(1);
            _tcpTimeout = TimeSpan.FromSeconds(5);
            _statsPeriod = TimeSpan.FromSeconds(15);
            _prepareTimeout = TimeSpan.FromSeconds(2);
            _commitTimeout = TimeSpan.FromSeconds(2);
            _gossipOnPublic = true;
            _adminOnPublic = true;
            _statsOnPublic = true;
            _disableScavengeMerging = false;
            _nodePriority = 1;
            _enableTrustedAuth = false;
            _authenticationProviderFactory = new InternalAuthenticationProviderFactory();
        }

        /// <summary>
        /// Returns a builder set to construct options for a single node instance
        /// </summary>
        /// <returns></returns>
        public static ClusterVNodeBuilder AsSingleNode()
        {
            var ret = new ClusterVNodeBuilder
            {
                _clusterNodeCount = 1,
                _prepareAckCount = 1,
                _commitAckCount = 1
            };
            return ret;
        }

        /// <summary>
        /// Returns a builder set to construct options for a cluster node instance with a cluster size 
        /// </summary>
        /// <returns></returns>
        public static ClusterVNodeBuilder AsClusterMember(int clusterSize)
        {
            int quorumSize = clusterSize/2;
            var ret = new ClusterVNodeBuilder
            {
                _clusterNodeCount = clusterSize,
                _prepareAckCount = quorumSize,
                _commitAckCount = quorumSize
            };
            return ret;
        }

        /// <summary>
        /// Sets the default endpoints on localhost (1113 tcp, 2113 http)
        /// </summary>
        /// <returns>A <see cref="ClusterVNodeBuilder"/> with the options set</returns>
        public ClusterVNodeBuilder OnDefaultEndpoints()
        {
            _externalHttp = new IPEndPoint(IPAddress.Loopback, 2113);
            _externalTcp = new IPEndPoint(IPAddress.Loopback, 1113);
            return this;
        }

        /// <summary>
        /// Sets the internal http endpoint to the specified value
        /// </summary>
        /// <returns>A <see cref="ClusterVNodeBuilder"/> with the options set</returns>
        public ClusterVNodeBuilder WithInternalHttpOn(IPEndPoint endpoint)
        {
            _internalHttp = endpoint;
            return this;
        }

        /// <summary>
        /// Sets the internal gossip port (used when using cluster dns, this should point to a known port gossip will be running on)
        /// </summary>
        /// <returns>A <see cref="ClusterVNodeBuilder"/> with the options set</returns>
        public ClusterVNodeBuilder ClusterGossipPortOf(int port)
        {
            _clusterGossipPort = port;
            return this;
        }

        /// <summary>
        /// Sets the external http endpoint to the specified value
        /// </summary>
        /// <returns>A <see cref="ClusterVNodeBuilder"/> with the options set</returns>
        public ClusterVNodeBuilder WithExternalHttpOn(IPEndPoint endpoint)
        {
            _externalHttp = endpoint;
            return this;
        }

        /// <summary>
        /// Sets the internal tcp endpoint to the specified value
        /// </summary>
        /// <returns>A <see cref="ClusterVNodeBuilder"/> with the options set</returns>
        public ClusterVNodeBuilder WithInternalTcpOn(IPEndPoint endpoint)
        {
            _internalTcp = endpoint;
            return this;
        }

        /// <summary>
        /// Sets the internal tcp endpoint to the specified value
        /// </summary>
        /// <returns>A <see cref="ClusterVNodeBuilder"/> with the options set</returns>
        public ClusterVNodeBuilder WithInternalSecureTcpOn(IPEndPoint endpoint)
        {
            _internalSecureTcp = endpoint;
            return this;
        }

        /// <summary>
        /// Sets the external tcp endpoint to the specified value
        /// </summary>
        /// <returns>A <see cref="ClusterVNodeBuilder"/> with the options set</returns>
        public ClusterVNodeBuilder WithExternalTcpOn(IPEndPoint endpoint)
        {
            _externalTcp = endpoint;
            return this;
        }

        /// <summary>
        /// Sets the external tcp endpoint to the specified value
        /// </summary>
        /// <returns>A <see cref="ClusterVNodeBuilder"/> with the options set</returns>
        public ClusterVNodeBuilder WithExternalSecureTcpOn(IPEndPoint endpoint)
        {
            _externalSecureTcp = endpoint;
            return this;
        }


        /// <summary>
        /// Sets that SSL should be used on connections
        /// </summary>
        /// <returns>A <see cref="ClusterVNodeBuilder"/> with the options set</returns>
        public ClusterVNodeBuilder EnableSSL()
        {
            _useSsl = true;
            return this;
        }

        /// <summary>
        /// Sets the certificate to be used with SSL
        /// </summary>
        /// <returns>A <see cref="ClusterVNodeBuilder"/> with the options set</returns>
        public ClusterVNodeBuilder WithCertificate(X509Certificate2 certificate)
        {
            _certificate = certificate;
            return this;
        }

        /// <summary>
        /// Sets the gossip seeds this node should talk to
        /// </summary>
        /// <param name="endpoints">The gossip seeds this node should try to talk to</param>
        /// <returns>A <see cref="ClusterVNodeBuilder"/> with the options set</returns>
        public ClusterVNodeBuilder WithGossipSeeds(params IPEndPoint[] endpoints)
        {
            _gossipSeeds = endpoints;
            _discoverViaDns = false;
            return this;
        }

        /// <summary>
        /// Sets the maximum size a memtable is allowed to reach (in count) before being moved to be a ptable
        /// </summary>
        /// <param name="size">The maximum count</param>
        /// <returns>A <see cref="ClusterVNodeBuilder"/> with the options set</returns>
        public ClusterVNodeBuilder MaximumMemoryTableSizeOf(int size)
        {
            _maxMemtableSize = size;
            return this;
        }

        /// <summary>
        /// Marks that the existing database files should not be checked for checksums on startup.
        /// </summary>
        /// <returns>A <see cref="ClusterVNodeBuilder"/> with the options set</returns>
        public ClusterVNodeBuilder DoNotVerifyDBHashes()
        {
            _verifyDbHashes = false;
            return this;
        }

        /// <summary>
        /// Disables gossip on the public (client) interface
        /// </summary>
        /// <returns>A <see cref="ClusterVNodeBuilder"/> with the options set</returns>
        public ClusterVNodeBuilder NoGossipOnPublicInterface()
        {
            _gossipOnPublic = false;
            return this;
        }

        /// <summary>
        /// Disables the admin interface on the public (client) interface
        /// </summary>
        /// <returns>A <see cref="ClusterVNodeBuilder"/> with the options set</returns>
        public ClusterVNodeBuilder NoAdminOnPublicInterface()
        {
            _adminOnPublic = false;
            return this;
        }

        /// <summary>
        /// Disables statistics screens on the public (client) interface
        /// </summary>
        /// <returns>A <see cref="ClusterVNodeBuilder"/> with the options set</returns>
        public ClusterVNodeBuilder NoStatsOnPublicInterface()
        {
            _adminOnPublic = false;
            return this;
        }

        /// <summary>
        /// Marks that the existing database files should be checked for checksums on startup.
        /// </summary>
        /// <returns>A <see cref="ClusterVNodeBuilder"/> with the options set</returns>
        public ClusterVNodeBuilder VerifyDBHashes()
        {
            _verifyDbHashes = true;
            return this;
        }

        /// <summary>
        /// Sets the dns name used for the discovery of other cluster nodes
        /// </summary>
        /// <param name="name">The dns name the node should use to discover gossip partners</param>
        /// <returns>A <see cref="ClusterVNodeBuilder"/> with the options set</returns>
        public ClusterVNodeBuilder WithClusterDnsName(string name)
        {
            _clusterDns = name;
            _discoverViaDns = true;
            return this;
        }

        /// <summary>
        /// Sets the number of worker threads to use in shared threadpool
        /// </summary>
        /// <param name="count">The number of worker threads</param>
        /// <returns>A <see cref="ClusterVNodeBuilder"/> with the options set</returns>
        public ClusterVNodeBuilder WithWorkerThreads(int count)
        {
            _workerThreads = count;
            return this;
        }

        /// <summary>
        /// Adds a http prefix for the external http endpoint
        /// </summary>
        /// <param name="prefix">The prefix to add</param>
        /// <returns>A <see cref="ClusterVNodeBuilder"/> with the options set</returns>
        public ClusterVNodeBuilder AddHttpPrefix(string prefix)
        {
            var prefixes = new List<string>();
            if(_httpPrefixes != null) prefixes.AddRange(_httpPrefixes);
            prefixes.Add(prefix);
            _httpPrefixes = prefixes.ToArray();
            return this;
        }

        public static implicit operator ClusterVNode(ClusterVNodeBuilder builder)
        {
            var settings = new ClusterVNodeSettings(Guid.NewGuid(),
                0,
                builder._internalTcp,
                builder._internalSecureTcp,
                builder._externalTcp,
                builder._externalSecureTcp,
                builder._internalHttp,
                builder._externalHttp,
                builder._httpPrefixes,
                builder._enableTrustedAuth,
                builder._certificate,
                builder._workerThreads,
                builder._discoverViaDns,
                builder._clusterDns,
                builder._gossipSeeds,
                builder._minFlushDelay,
                builder._clusterNodeCount,
                builder._prepareAckCount,
                builder._commitAckCount,
                builder._prepareTimeout,
                builder._commitTimeout,
                builder._useSsl,
                builder._sslTargetHost,
                builder._sslValidateServer,
                builder._statsPeriod,
                builder._statsStorage,
                builder._nodePriority,
                builder._authenticationProviderFactory,
                builder._disableScavengeMerging,
                builder._adminOnPublic,
                builder._statsOnPublic,
                builder._gossipOnPublic,
                builder._gossipInterval,
                builder._gossipAllowedTimeDifference,
                builder._gossipTimeout,
                builder._tcpTimeout,
                builder._verifyDbHashes,
                builder._maxMemtableSize);
            return new ClusterVNode(null, settings, builder.GetGossipSource(), builder._subsystems.ToArray());
        }

        private IGossipSeedSource GetGossipSource()
        {
            IGossipSeedSource gossipSeedSource;
            if (_discoverViaDns)
            {
                gossipSeedSource = new DnsGossipSeedSource(_clusterDns, _clusterGossipPort);
            }
            else
            {
                if (_gossipSeeds == null || _gossipSeeds.Length == 0)
                {
                    if (_clusterNodeCount > 1)
                    {
                        throw new Exception("DNS discovery is disabled, but no gossip seed endpoints have been specified. "
                            + "Specify gossip seeds");
                    }
                    else
                    {
                        Console.WriteLine("DNS discovery is disabled, but no gossip seed endpoints have been specified. Since"
                            + "the cluster size is set to 1, this may be intentional. Gossip seeds can be specified"
                            + "seeds using the --gossip-seed command line option.");
                    }
                }

                gossipSeedSource = new KnownEndpointGossipSeedSource(_gossipSeeds);
            }
            return gossipSeedSource;
        }
    }
}