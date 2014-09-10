﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Utils;
using EventStore.Core;
using EventStore.Core.Authentication;
using EventStore.Core.Cluster.Settings;
using EventStore.Core.Services.Gossip;
using EventStore.Core.Services.Monitoring;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.Util;

namespace EventStore.ClientAPI.Embedded
{
    public class EmbeddedVNodeBuilder
    {
        // ReSharper disable FieldCanBeMadeReadOnly.Local - as more options are added
        private int _chunkSize;
        private string _dbPath;
        private long _chunksCacheSize;
        private bool _inMemoryDb;

        private IPEndPoint _internalTcp;
        private IPEndPoint _internalSecureTcp;
        private IPEndPoint _externalTcp;
        private IPEndPoint _externalSecureTcp;
        private IPEndPoint _internalHttp;
        private IPEndPoint _externalHttp;

        private List<string> _httpPrefixes;
        private bool _enableTrustedAuth;
        private X509Certificate2 _certificate;
        private int _workerThreads;

        private bool _discoverViaDns;
        private string _clusterDns;
        private List<IPEndPoint> _gossipSeeds;

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
        private bool _skipVerifyDbHashes;
        private int _maxMemtableSize;
        private List<ISubsystem> _subsystems;
        private int _clusterGossipPort;
        // ReSharper restore FieldCanBeMadeReadOnly.Local

        private EmbeddedVNodeBuilder()
        {
            _chunkSize = TFConsts.ChunkSize;
            _dbPath = Path.Combine(Path.GetTempPath(), "EmbeddedEventStore", string.Format("{0:yyyy-MM-dd_HH.mm.ss.ffffff}-EmbeddedNode", DateTime.UtcNow));
            _chunksCacheSize = TFConsts.ChunksCacheSize;
            _inMemoryDb = true;

            _externalTcp = new IPEndPoint(Opts.ExternalIpDefault, Opts.ExternalHttpPortDefault);
            _externalSecureTcp = null;
            _externalTcp = new IPEndPoint(Opts.ExternalIpDefault, Opts.ExternalHttpPortDefault);
            _externalSecureTcp = null;
            _externalHttp = new IPEndPoint(Opts.ExternalIpDefault, Opts.ExternalHttpPortDefault);
            _externalHttp = new IPEndPoint(Opts.ExternalIpDefault, Opts.ExternalHttpPortDefault);

            _httpPrefixes = new List<string>();
            _enableTrustedAuth = Opts.EnableTrustedAuthDefault;
            _certificate = null;
            _workerThreads = Opts.WorkerThreadsDefault;

            _discoverViaDns = false;
            _clusterDns = Opts.ClusterDnsDefault;
            _gossipSeeds = new List<IPEndPoint>();

            _minFlushDelay = TimeSpan.FromMilliseconds(Opts.MinFlushDelayMsDefault);

            _clusterNodeCount = 1;
            _prepareAckCount = 1;
            _commitAckCount = 1;
            _prepareTimeout = TimeSpan.FromMilliseconds(Opts.PrepareTimeoutMsDefault);
            _commitTimeout = TimeSpan.FromMilliseconds(Opts.CommitTimeoutMsDefault);

            _nodePriority = Opts.NodePriorityDefault;

            _useSsl = Opts.UseInternalSslDefault;
            _sslTargetHost = Opts.SslTargetHostDefault;
            _sslValidateServer = Opts.SslValidateServerDefault;

            _statsPeriod = TimeSpan.FromSeconds(Opts.StatsPeriodDefault);
            _statsStorage = StatsStorage.Stream;

            _authenticationProviderFactory = new InternalAuthenticationProviderFactory();
            _disableScavengeMerging = Opts.DisableScavengeMergeDefault;
            _adminOnPublic = Opts.AdminOnExtDefault;
            _statsOnPublic = Opts.StatsOnExtDefault;
            _gossipOnPublic = Opts.GossipOnExtDefault;
            _gossipInterval = TimeSpan.FromMilliseconds(Opts.GossipIntervalMsDefault);
            _gossipAllowedTimeDifference = TimeSpan.FromMilliseconds(Opts.GossipAllowedDifferenceMsDefault);
            _gossipTimeout = TimeSpan.FromMilliseconds(Opts.GossipTimeoutMsDefault);
            _tcpTimeout = TimeSpan.FromMilliseconds(Opts.TcpTimeoutDefault);
            _skipVerifyDbHashes = Opts.SkipDbVerifyDefault;
            _maxMemtableSize = Opts.MaxMemtableSizeDefault;
            _subsystems = new List<ISubsystem>();
            _clusterGossipPort = Opts.ClusterGossipPortDefault;
        }

        /// <summary>
        /// Returns a builder set to construct options for a single node instance
        /// </summary>
        /// <returns></returns>
        public static EmbeddedVNodeBuilder AsSingleNode()
        {
            var ret = new EmbeddedVNodeBuilder
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
        public static EmbeddedVNodeBuilder AsClusterMember(int clusterSize)
        {
            int quorumSize = clusterSize/2;
            var ret = new EmbeddedVNodeBuilder
            {
                _clusterNodeCount = clusterSize,
                _prepareAckCount = quorumSize,
                _commitAckCount = quorumSize
            };
            return ret;
        }

        public EmbeddedVNodeBuilder RunInMemory()
        {
            _inMemoryDb = true;
            _dbPath = Path.Combine(Path.GetTempPath(), "EmbeddedEventStore", string.Format("{0:yyyy-MM-dd_HH.mm.ss.ffffff}-EmbeddedNode", DateTime.UtcNow));
            return this;
        }

        public EmbeddedVNodeBuilder RunOnDisk(string path)
        {
            _inMemoryDb = false;
            _dbPath = path;
            return this;
        }

        /// <summary>
        /// Sets the default endpoints on localhost (1113 tcp, 2113 http)
        /// </summary>
        /// <returns>A <see cref="EmbeddedVNodeBuilder"/> with the options set</returns>
        public EmbeddedVNodeBuilder OnDefaultEndpoints()
        {
            _internalHttp = new IPEndPoint(Opts.InternalIpDefault, 2112);
            _internalTcp = new IPEndPoint(Opts.InternalIpDefault, 1112);
            _externalHttp = new IPEndPoint(Opts.ExternalIpDefault, 2113);
            _externalTcp = new IPEndPoint(Opts.InternalIpDefault, 1113);
            return this;
        }

        /// <summary>
        /// Sets the internal http endpoint to the specified value
        /// </summary>
        /// <returns>A <see cref="EmbeddedVNodeBuilder"/> with the options set</returns>
        public EmbeddedVNodeBuilder WithInternalHttpOn(IPEndPoint endpoint)
        {
            _internalHttp = endpoint;
            return this;
        }

        /// <summary>
        /// Sets the internal gossip port (used when using cluster dns, this should point to a known port gossip will be running on)
        /// </summary>
        /// <returns>A <see cref="EmbeddedVNodeBuilder"/> with the options set</returns>
        public EmbeddedVNodeBuilder WithClusterGossipPort(int port)
        {
            _clusterGossipPort = port;
            return this;
        }

        /// <summary>
        /// Sets the external http endpoint to the specified value
        /// </summary>
        /// <returns>A <see cref="EmbeddedVNodeBuilder"/> with the options set</returns>
        public EmbeddedVNodeBuilder WithExternalHttpOn(IPEndPoint endpoint)
        {
            _externalHttp = endpoint;
            return this;
        }

        /// <summary>
        /// Sets the internal tcp endpoint to the specified value
        /// </summary>
        /// <returns>A <see cref="EmbeddedVNodeBuilder"/> with the options set</returns>
        public EmbeddedVNodeBuilder WithInternalTcpOn(IPEndPoint endpoint)
        {
            _internalTcp = endpoint;
            return this;
        }

        /// <summary>
        /// Sets the internal tcp endpoint to the specified value
        /// </summary>
        /// <returns>A <see cref="EmbeddedVNodeBuilder"/> with the options set</returns>
        public EmbeddedVNodeBuilder WithInternalSecureTcpOn(IPEndPoint endpoint)
        {
            _internalSecureTcp = endpoint;
            return this;
        }

        /// <summary>
        /// Sets the external tcp endpoint to the specified value
        /// </summary>
        /// <returns>A <see cref="EmbeddedVNodeBuilder"/> with the options set</returns>
        public EmbeddedVNodeBuilder WithExternalTcpOn(IPEndPoint endpoint)
        {
            _externalTcp = endpoint;
            return this;
        }

        /// <summary>
        /// Sets the external tcp endpoint to the specified value
        /// </summary>
        /// <returns>A <see cref="EmbeddedVNodeBuilder"/> with the options set</returns>
        public EmbeddedVNodeBuilder WithExternalSecureTcpOn(IPEndPoint endpoint)
        {
            _externalSecureTcp = endpoint;
            return this;
        }

        /// <summary>
        /// Sets that SSL should be used on connections
        /// </summary>
        /// <returns>A <see cref="EmbeddedVNodeBuilder"/> with the options set</returns>
        public EmbeddedVNodeBuilder EnableSsl()
        {
            _useSsl = true;
            return this;
        }

        /// <summary>
        /// Sets the gossip seeds this node should talk to
        /// </summary>
        /// <param name="endpoints">The gossip seeds this node should try to talk to</param>
        /// <returns>A <see cref="EmbeddedVNodeBuilder"/> with the options set</returns>
        public EmbeddedVNodeBuilder WithGossipSeeds(params IPEndPoint[] endpoints)
        {
            _gossipSeeds.Clear();
            _gossipSeeds.AddRange(endpoints);
            _discoverViaDns = false;
            return this;
        }

        /// <summary>
        /// Sets the maximum size a memtable is allowed to reach (in count) before being moved to be a ptable
        /// </summary>
        /// <param name="size">The maximum count</param>
        /// <returns>A <see cref="EmbeddedVNodeBuilder"/> with the options set</returns>
        public EmbeddedVNodeBuilder MaximumMemoryTableSizeOf(int size)
        {
            _maxMemtableSize = size;
            return this;
        }

        /// <summary>
        /// Marks that the existing database files should not be checked for checksums on startup.
        /// </summary>
        /// <returns>A <see cref="EmbeddedVNodeBuilder"/> with the options set</returns>
        public EmbeddedVNodeBuilder DoNotVerifyDbHashes()
        {
            _skipVerifyDbHashes = false;
            return this;
        }

        /// <summary>
        /// Disables gossip on the public (client) interface
        /// </summary>
        /// <returns>A <see cref="EmbeddedVNodeBuilder"/> with the options set</returns>
        public EmbeddedVNodeBuilder NoGossipOnPublicInterface()
        {
            _gossipOnPublic = false;
            return this;
        }

        /// <summary>
        /// Disables the admin interface on the public (client) interface
        /// </summary>
        /// <returns>A <see cref="EmbeddedVNodeBuilder"/> with the options set</returns>
        public EmbeddedVNodeBuilder NoAdminOnPublicInterface()
        {
            _adminOnPublic = false;
            return this;
        }

        /// <summary>
        /// Disables statistics screens on the public (client) interface
        /// </summary>
        /// <returns>A <see cref="EmbeddedVNodeBuilder"/> with the options set</returns>
        public EmbeddedVNodeBuilder NoStatsOnPublicInterface()
        {
            _adminOnPublic = false;
            return this;
        }

        /// <summary>
        /// Marks that the existing database files should be checked for checksums on startup.
        /// </summary>
        /// <returns>A <see cref="EmbeddedVNodeBuilder"/> with the options set</returns>
        public EmbeddedVNodeBuilder VerifyDbHashes()
        {
            _skipVerifyDbHashes = true;
            return this;
        }

        /// <summary>
        /// Sets the dns name used for the discovery of other cluster nodes
        /// </summary>
        /// <param name="name">The dns name the node should use to discover gossip partners</param>
        /// <returns>A <see cref="EmbeddedVNodeBuilder"/> with the options set</returns>
        public EmbeddedVNodeBuilder WithClusterDnsName(string name)
        {
            _clusterDns = name;
            _discoverViaDns = true;
            return this;
        }

        /// <summary>
        /// Sets the number of worker threads to use in shared threadpool
        /// </summary>
        /// <param name="count">The number of worker threads</param>
        /// <returns>A <see cref="EmbeddedVNodeBuilder"/> with the options set</returns>
        public EmbeddedVNodeBuilder WithWorkerThreads(int count)
        {
            _workerThreads = count;
            return this;
        }

        /// <summary>
        /// Adds a http prefix for the external http endpoint
        /// </summary>
        /// <param name="prefix">The prefix to add</param>
        /// <returns>A <see cref="EmbeddedVNodeBuilder"/> with the options set</returns>
        public EmbeddedVNodeBuilder AddHttpPrefix(string prefix)
        {
            _httpPrefixes.Add(prefix);
            return this;
        }
       
 	/// <summary>
 	/// Sets the Server SSL Certificate to be loaded from a file
 	/// </summary>
 	/// <param name="path">The path to the certificate file</param>
 	/// <param name="password">The password for the certificate</param>
        /// <returns>A <see cref="EmbeddedVNodeBuilder"/> with the options set</returns>
        public EmbeddedVNodeBuilder WithServerCertificateFromFile(string path, string password)
        {
            var cert = new X509Certificate2(path, password);

            _certificate = cert;
            return this;
        }

	/// <summary>
	/// Sets the Server SSL Certificate to be loaded from a certificate store
	/// </summary>
	/// <param name="storeLocation">The location of the certificate store</param>
	/// <param name="storeName">The name of the certificate store</param>
	/// <param name="certificateSubjectName">The subject name of the certificate</param>
	/// <param name="certificateThumbprint">The thumbpreint of the certificate</param>
        /// <returns>A <see cref="EmbeddedVNodeBuilder"/> with the options set</returns>
        public EmbeddedVNodeBuilder WithServerCertificateFromStore(StoreLocation storeLocation, StoreName storeName, string certificateSubjectName, string certificateThumbprint)
        {
            var store = new X509Store(storeName, storeLocation);

            try
            {
                store.Open(OpenFlags.OpenExistingOnly);
            }
            catch (Exception exc)
            {
                throw new Exception(string.Format("Couldn't open certificate store '{0}' in location {1}'.", storeName, storeLocation), exc);
            }

            if (!string.IsNullOrWhiteSpace(certificateThumbprint))
            {
                var certificates = store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint, true);
                if (certificates.Count == 0)
                    throw new Exception(string.Format("Could not find valid certificate with thumbprint '{0}'.", certificateThumbprint));
                
                //Can this even happen?
                if (certificates.Count > 1)
                    throw new Exception(string.Format("Cannot determine a unique certificate from thumbprint '{0}'.", certificateThumbprint));

                _certificate = certificates[0];
                return this;
            }
            
            if (!string.IsNullOrWhiteSpace(certificateSubjectName))
            {
                var certificates = store.Certificates.Find(X509FindType.FindBySubjectName, certificateSubjectName, true);
                if (certificates.Count == 0)
                    throw new Exception(string.Format("Could not find valid certificate with thumbprint '{0}'.", certificateThumbprint));

                //Can this even happen?
                if (certificates.Count > 1)
                    throw new Exception(string.Format("Cannot determine a unique certificate from thumbprint '{0}'.", certificateThumbprint));

                _certificate = certificates[0];
                return this;
            }
            
            throw new ArgumentException("No thumbprint or subject name was specified for a certificate, but a certificate store was specified.");
        }

        private void EnsureHttpPrefixes()
        {
            if (_httpPrefixes == null || _httpPrefixes.IsEmpty())
                _httpPrefixes = new List<string>(new[] {_externalHttp.ToHttpUrl()});

            if (!Runtime.IsMono) 
                return;

            if (!_httpPrefixes.Contains(x => x.Contains("localhost")) && Equals(_externalHttp.Address, IPAddress.Loopback))
            {
                _httpPrefixes.Add(string.Format("http://localhost:{0}/", _externalHttp.Port));
            }
        }

        public static implicit operator ClusterVNode(EmbeddedVNodeBuilder builder)
        {
            builder.EnsureHttpPrefixes();

            var dbConfig = CreateDbConfig(builder._chunkSize, builder._dbPath, builder._chunksCacheSize,
                builder._inMemoryDb);
            var db = new TFChunkDb(dbConfig);

            var vNodeSettings = new ClusterVNodeSettings(Guid.NewGuid(),
                0,
                builder._internalTcp,
                builder._internalSecureTcp,
                builder._externalTcp,
                builder._externalSecureTcp,
                builder._internalHttp,
                builder._externalHttp,
                builder._httpPrefixes.ToArray(),
                builder._enableTrustedAuth,
                builder._certificate,
                builder._workerThreads,
                builder._discoverViaDns,
                builder._clusterDns,
                builder._gossipSeeds.ToArray(),
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
                builder._skipVerifyDbHashes,
                builder._maxMemtableSize);
            return new ClusterVNode(db, vNodeSettings, builder.GetGossipSource(), builder._subsystems.ToArray());
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
                if ((_gossipSeeds == null || _gossipSeeds.Count == 0) && _clusterNodeCount > 1)
                {
                    throw new Exception("DNS discovery is disabled, but no gossip seed endpoints have been specified. "
                        + "Specify gossip seeds");
                }

		if (_gossipSeeds == null)
		    throw new ApplicationException("Gossip seeds cannot be null");
                gossipSeedSource = new KnownEndpointGossipSeedSource(_gossipSeeds.ToArray());
            }
            return gossipSeedSource;
        }
        
        private static TFChunkDbConfig CreateDbConfig(int chunkSize, string dbPath, long chunksCacheSize, bool inMemDb)
        {
            ICheckpoint writerChk;
            ICheckpoint chaserChk;
            ICheckpoint epochChk;
            ICheckpoint truncateChk;
            if (inMemDb)
            {
                writerChk = new InMemoryCheckpoint(Checkpoint.Writer);
                chaserChk = new InMemoryCheckpoint(Checkpoint.Chaser);
                epochChk = new InMemoryCheckpoint(Checkpoint.Epoch, initValue: -1);
                truncateChk = new InMemoryCheckpoint(Checkpoint.Truncate, initValue: -1);
            }
            else
            {
                var writerCheckFilename = Path.Combine(dbPath, Checkpoint.Writer + ".chk");
                var chaserCheckFilename = Path.Combine(dbPath, Checkpoint.Chaser + ".chk");
                var epochCheckFilename = Path.Combine(dbPath, Checkpoint.Epoch + ".chk");
                var truncateCheckFilename = Path.Combine(dbPath, Checkpoint.Truncate + ".chk");
                if (Runtime.IsMono)
                {
                    writerChk = new FileCheckpoint(writerCheckFilename, Checkpoint.Writer, cached: true);
                    chaserChk = new FileCheckpoint(chaserCheckFilename, Checkpoint.Chaser, cached: true);
                    epochChk = new FileCheckpoint(epochCheckFilename, Checkpoint.Epoch, cached: true, initValue: -1);
                    truncateChk = new FileCheckpoint(truncateCheckFilename, Checkpoint.Truncate, cached: true, initValue: -1);
                }
                else
                {
                    writerChk = new MemoryMappedFileCheckpoint(writerCheckFilename, Checkpoint.Writer, cached: true);
                    chaserChk = new MemoryMappedFileCheckpoint(chaserCheckFilename, Checkpoint.Chaser, cached: true);
                    epochChk = new MemoryMappedFileCheckpoint(epochCheckFilename, Checkpoint.Epoch, cached: true, initValue: -1);
                    truncateChk = new MemoryMappedFileCheckpoint(truncateCheckFilename, Checkpoint.Truncate, cached: true, initValue: -1);
                }
            }
            var nodeConfig = new TFChunkDbConfig(dbPath,
                                                 new VersionedPatternFileNamingStrategy(dbPath, "chunk-"),
                                                 chunkSize,
                                                 chunksCacheSize,
                                                 writerChk,
                                                 chaserChk,
                                                 epochChk,
                                                 truncateChk,
                                                 inMemDb);
            return nodeConfig;
        }
    }
}