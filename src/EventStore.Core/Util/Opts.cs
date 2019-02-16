using System.Net;
using EventStore.Common.Options;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Util {
	public static class Opts {
		public const string EnvPrefix = "EVENTSTORE_";

		/*
		 * OPTIONS GROUPS
		 */
		public const string AppGroup = "Application Options";
		public const string DbGroup = "Database Options";
		public const string ProjectionsGroup = "Projections Options";
		public const string AuthGroup = "Authentication Options";
		public const string InterfacesGroup = "Interface Options";
		public const string CertificatesGroup = "Certificate Options";
		public const string ClusterGroup = "Cluster Options";
		public const string ManagerGroup = "Manager Options";

		/*
		 *  COMMON OPTIONS
		 */

		public const string ForceDescr =
			"Force the Event Store to run in possibly harmful environments such as with Boehm GC.";

		public const bool ForceDefault = false;

		public const string WhatIfDescr = "Print effective configuration to console and then exit.";
		public const bool WhatIfDefault = false;

		public const string StartStandardProjectionsDescr = "Start the built in system projections.";
		public const bool StartStandardProjectionsDefault = false;
		public const string DisableHttpCachingDescr = "Disable HTTP caching.";
		public const bool DisableHttpCachingDefault = false;

		public const string LogsDescr = "Path where to keep log files.";
		public const string StructuredLogDescr = "Enable structured logging.";
		public const bool StructuredLogDefault = true;

		public const string ConfigsDescr = "Configuration files.";
		public static readonly string[] ConfigsDefault = new string[0];

		public const string DefinesDescr = "Run-time conditionals.";
		public static readonly string[] DefinesDefault = new string[0];

		public const string ShowHelpDescr = "Show help.";
		public const bool ShowHelpDefault = false;

		public const string ShowVersionDescr = "Show version.";
		public const bool ShowVersionDefault = false;

		public const string MonoMinThreadpoolSizeDescr =
			"Minimum number of worker threads when running under mono. Set to 0 to leave machine defaults.";

		public const int MonoMinThreadpoolSizeDefault = 10;

		public const string ExtTcpHeartbeatTimeoutDescr = "Heartbeat timeout for external TCP sockets";
		public const int ExtTcpHeartbeatTimeoutDefault = 1000;

		public const string IntTcpHeartbeatTimeoutDescr = "Heartbeat timeout for internal TCP sockets";
		public const int IntTcpHeartbeatTimeoutDefault = 700;

		public const string ExtTcpHeartbeatIntervalDescr = "Heartbeat interval for external TCP sockets";
		public const int ExtTcpHeartbeatIntervalDefault = 2000;

		public const string IntTcpHeartbeatIntervalDescr = "Heartbeat interval for internal TCP sockets";
		public const int IntTcpHeartbeatIntervalDefault = 700;

		public const string ConnectionPendingSendBytesThresholdDescr =
			"The maximum number of pending send bytes allowed before a connection is closed.";

		public const int ConnectionPendingSendBytesThresholdDefault = 10 * 1024 * 1024;

		public const string GossipOnSingleNodeDescr =
			"When enabled tells a single node to run gossip as if it is a cluster";

		public const bool GossipOnSingleNodeDefault = false;

		public const string StatsPeriodDescr = "The number of seconds between statistics gathers.";
		public const int StatsPeriodDefault = 30;

		public const string CachedChunksDescr = "The number of chunks to cache in unmanaged memory.";
		public const int CachedChunksDefault = -1;

		public const string ChunksCacheSizeDescr = "The amount of unmanaged memory to use for caching chunks in bytes.";
		public const int ChunksCacheSizeDefault = TFConsts.ChunksCacheSize;

		public const string MinFlushDelayMsDescr = "The minimum flush delay in milliseconds.";
		public static double MinFlushDelayMsDefault = TFConsts.MinFlushDelayMs.TotalMilliseconds;

		public const string NodePriorityDescr = "The node priority used during master election";
		public const int NodePriorityDefault = 0;

		public const string DisableScavengeMergeDescr = "Disables the merging of chunks when scavenge is running";
		public static readonly bool DisableScavengeMergeDefault = false;

		public const string ScavengeHistoryMaxAgeDescr = "The number of days to keep scavenge history";
		public static readonly int ScavengeHistoryMaxAgeDefault = 30;

		public const string DbPathDescr = "The path the db should be loaded/saved to.";
		public const string IndexPathDescr = "The path the index should be loaded/saved to.";

		public const string InMemDbDescr = "Keep everything in memory, no directories or files are created.";
		public const bool InMemDbDefault = false;

		public const string EnableTrustedAuthDescr = "Enables trusted authentication by an intermediary in the HTTP";
		public const bool EnableTrustedAuthDefault = false;

		public const string AddInterfacePrefixesDescr = "Add interface prefixes";
		public const bool AddInterfacePrefixesDefault = true;

		public const string MaxMemTableSizeDescr = "Adjusts the maximum size of a mem table.";
		public const int MaxMemtableSizeDefault = 1000000;

		public const string HashCollisionReadLimitDescr =
			"The number of events to read per candidate in the case of a hash collision";

		public const int HashCollisionReadLimitDefault = 100;

		public const string SkipDbVerifyDescr =
			"Bypasses the checking of file hashes of database during startup (allows for faster startup).";

		public const bool SkipDbVerifyDefault = false;

		public const string WriteThroughDescr =
			"Enables Write Through when writing to the file system, this bypasses filesystem caches.";

		public const bool WriteThroughDefault = false;

		public const string ChunkInitialReaderCountDescr =
			"The initial number of readers to start when opening a TFChunk";

		public const int ChunkInitialReaderCountDefault = 5;

		public const string UnbufferedDescr =
			"Enables Unbuffered/DirectIO when writing to the file system, this bypasses filesystem caches.";

		public const bool UnbufferedDefault = false;

		public const string ReaderThreadsCountDescr = "The number of reader threads to use for processing reads.";
		public const int ReaderThreadsCountDefault = 4;

		public const string RunProjectionsDescr =
			"Enables the running of projections. System runs built-in projections, All runs user projections.";

		public const ProjectionType RunProjectionsDefault = ProjectionType.None;

		public const string ProjectionThreadsDescr = "The number of threads to use for projections.";
		public const int ProjectionThreadsDefault = 3;

		public const string FaultOutOfOrderProjectionsDescr =
			"Fault the projection if the Event number that was expected in the stream differs from what is received. This may happen if events have been deleted or expired";

		public const bool FaultOutOfOrderProjectionsDefault = false;

		public const string ProjectionsQueryExpiryDescr = "The number of minutes a query can be idle before it expires";
		public const int ProjectionsQueryExpiryDefault = 5;

		public const string WorkerThreadsDescr = "The number of threads to use for pool of worker services.";
		public const int WorkerThreadsDefault = 5;

		public const string IntHttpPrefixesDescr = "The prefixes that the internal HTTP server should respond to.";
		public static readonly string[] IntHttpPrefixesDefault = new string[0];

		public const string ExtHttpPrefixesDescr = "The prefixes that the external HTTP server should respond to.";
		public static readonly string[] ExtHttpPrefixesDefault = new string[0];

		public const string UnsafeIgnoreHardDeleteDescr = "Disables Hard Deletes (UNSAFE: use to remove hard deletes)";
		public static readonly bool UnsafeIgnoreHardDeleteDefault = false;

		public const string AlwaysKeepScavengedDescr = "Always keeps the newer chunks from a scavenge operation.";
		public static readonly bool AlwaysKeepScavengedDefault = false;

		public const string UnsafeDisableFlushToDiskDescr = "Disable flushing to disk.  (UNSAFE: on power off)";
		public static readonly bool UnsafeDisableFlushToDiskDefault = false;

		public const string PrepareTimeoutMsDescr = "Prepare timeout (in milliseconds).";
		public static readonly int PrepareTimeoutMsDefault = 2000; // 2 seconds

		public const string CommitTimeoutMsDescr = "Commit timeout (in milliseconds).";
		public static readonly int CommitTimeoutMsDefault = 2000; // 2 seconds

		public const string BetterOrderingDescr =
			"Enable Queue affinity on reads during write process to try to get better ordering.";

		public static readonly bool BetterOrderingDefault = false;

		public const string LogHttpRequestsDescr = "Log Http Requests and Responses before processing them.";
		public static readonly bool LogHttpRequestsDefault = false;

		public const string SkipIndexScanOnReadsDescr =
			"Skip Index Scan on Reads. This skips the index scan which was used to stop reading duplicates.";

		public static readonly bool SkipIndexScanOnReadsDefault = false;

		public const string ReduceFileCachePressureDescr =
			"Change the way the DB files are opened to reduce their stickiness in the system file cache.";

		public static readonly bool ReduceFileCachePressureDefault = false;

		public const string InitializationThreadsDescr =
			"Number of threads to be used to initialize the database. Will be capped at host processor count.";

		public static readonly int InitializationThreadsDefault = 1;

		//Loading certificates from files
		public const string CertificateFileDescr = "The path to certificate file.";
		public static readonly string CertificateFileDefault = string.Empty;

		public const string CertificatePasswordDescr = "The password to certificate in file.";
		public static readonly string CertificatePasswordDefault = string.Empty;

		//Loading certificates from a certificate store
		public const string CertificateStoreLocationDescr = "The certificate store location name.";
		public static readonly string CertificateStoreLocationDefault = string.Empty;

		public const string CertificateStoreNameDescr = "The certificate store name.";
		public static readonly string CertificateStoreNameDefault = string.Empty;

		public const string CertificateSubjectNameDescr = "The certificate subject name.";
		public static readonly string CertificateSubjectNameDefault = string.Empty;

		public const string CertificateThumbprintDescr = "The certificate fingerprint/thumbprint.";
		public static readonly string CertificateThumbprintDefault = string.Empty;

		/*
		 *  SINGLE NODE OPTIONS
		 */
		public const string IpDescr = "The IP address to bind to.";
		public static readonly IPAddress IpDefault = IPAddress.Loopback;

		public const string TcpPortDescr = "The port to run the TCP server on.";
		public const int TcpPortDefault = 1113;

		public const string SecureTcpPortDescr = "The port to run the secure TCP server on.";
		public const int SecureTcpPortDefault = 0;

		public const string HttpPortDescr = "The port to run the HTTP server on.";
		public const int HttpPortDefault = 2113;

		/*
		 *  CLUSTER OPTIONS
		 */
		public const string GossipAllowedDifferenceMsDescr =
			"The amount of drift, in ms, between clocks on nodes allowed before gossip is rejected.";

		public const int GossipAllowedDifferenceMsDefault = 60000;

		public const string GossipIntervalMsDescr = "The interval, in ms, nodes should try to gossip with each other.";
		public const int GossipIntervalMsDefault = 1000;

		public const string GossipTimeoutMsDescr = "The timeout, in ms, on gossip to another node.";
		public const int GossipTimeoutMsDefault = 500;

		public const string AdminOnExtDescr = "Whether or not to run the admin ui on the external HTTP endpoint";
		public const bool AdminOnExtDefault = true;

		public const string GossipOnExtDescr = "Whether or not to accept gossip requests on the external HTTP endpoint";
		public const bool GossipOnExtDefault = true;

		public const string StatsOnExtDescr =
			"Whether or not to accept statistics requests on the external HTTP endpoint, needed if you use admin ui";

		public const bool StatsOnExtDefault = true;

		public const string InternalIpDescr = "Internal IP Address.";
		public static readonly IPAddress InternalIpDefault = IPAddress.Loopback;

		public const string ExternalIpDescr = "External IP Address.";
		public static readonly IPAddress ExternalIpDefault = IPAddress.Loopback;

		public const string InternalHttpPortDescr = "Internal HTTP Port.";
		public const int InternalHttpPortDefault = 2112;

		public const string ExternalHttpPortDescr = "External HTTP Port.";
		public const int ExternalHttpPortDefault = 2113;

		public const string InternalTcpPortDescr = "Internal TCP Port.";
		public const int InternalTcpPortDefault = 1112;

		public const string InternalSecureTcpPortDescr = "Internal Secure TCP Port.";
		public const int InternalSecureTcpPortDefault = 0;

		public const string ExternalTcpPortDescr = "External TCP Port.";
		public const int ExternalTcpPortDefault = 1113;

		public const string ExternalSecureTcpPortDescr = "External Secure TCP Port.";
		public const int ExternalSecureTcpPortDefault = 0;

		public const string ExternalIpAdvertiseAsDescr = "Advertise External Tcp Address As.";
		public static readonly IPAddress ExternalIpAdvertiseAsDefault = null;

		public const string ExternalSecureTcpPortAdvertiseAsDescr = "Advertise Secure External Tcp Port As.";
		public static readonly int ExternalSecureTcpPortAdvertiseAsDefault = 0;

		public const string ExternalTcpPortAdvertiseAsDescr = "Advertise External Tcp Port As.";
		public static readonly int ExternalTcpPortAdvertiseAsDefault = 0;

		public const string ExternalHttpPortAdvertiseAsDescr = "Advertise External Http Port As.";
		public static readonly int ExternalHttpPortAdvertiseAsDefault = 0;

		public const string InternalIpAdvertiseAsDescr = "Advertise Internal Tcp Address As.";
		public static readonly IPAddress InternalIpAdvertiseAsDefault = null;

		public const string InternalTcpPortAdvertiseAsDescr = "Advertise Internal Tcp Port As.";
		public static readonly int InternalTcpPortAdvertiseAsDefault = 0;

		public const string InternalSecureTcpPortAdvertiseAsDescr = "Advertise Secure Internal Tcp Port As.";
		public static readonly int InternalSecureTcpPortAdvertiseAsDefault = 0;

		public const string InternalHttpPortAdvertiseAsDescr = "Advertise Internal Http Port As.";
		public static readonly int InternalHttpPortAdvertiseAsDefault = 0;

		public const string ClusterSizeDescr = "The number of nodes in the cluster.";
		public const int ClusterSizeDefault = 1;

		public const string CommitCountDescr =
			"The number of nodes which must acknowledge commits before acknowledging to a client.";

		public const int CommitCountDefault = -1;

		public const string PrepareCountDescr = "The number of nodes which must acknowledge prepares.";
		public const int PrepareCountDefault = -1;

		public const string InternalManagerIpDescr = null;
		public static readonly IPAddress InternalManagerIpDefault = IPAddress.Loopback;

		public const string ExternalManagerIpDescr = null;
		public static readonly IPAddress ExternalManagerIpDefault = IPAddress.Loopback;

		public const string InternalManagerHttpPortDescr = null;
		public const int InternalManagerHttpPortDefault = 30777;

		public const string ExternalManagerHttpPortDescr = null;
		public const int ExternalManagerHttpPortDefault = 30778;

		public const string UseInternalSslDescr = "Whether to use secure internal communication.";
		public const bool UseInternalSslDefault = false;

		public const string DisableInsecureTCPDescr = "Whether to disable insecure TCP communication";
		public const bool DisableInsecureTCPDefault = false;

		public const string SslTargetHostDescr = "Target host of server's SSL certificate.";
		public static readonly string SslTargetHostDefault = "n/a";

		public const string SslValidateServerDescr = "Whether to validate that server's certificate is trusted.";
		public const bool SslValidateServerDefault = true;

		public const string DiscoverViaDnsDescr = "Whether to use DNS lookup to discover other cluster nodes.";
		public const bool DiscoverViaDnsDefault = true;

		public const string ClusterDnsDescr = "DNS name from which other nodes can be discovered.";
		public const string ClusterDnsDefault = "fake.dns";

		public const int ClusterGossipPortDefault = 30777;
		public const string ClusterGossipPortDescr = "The port on which cluster nodes' managers are running.";

		public const string GossipSeedDescr = "Endpoints for other cluster nodes from which to seed gossip";
		public static readonly IPEndPoint[] GossipSeedDefault = new IPEndPoint[0];

		/*
		 *  MANAGER OPTIONS
		 */
		public const string EnableWatchdogDescr = "Enable the node supervisor";
		public const bool EnableWatchdogDefault = true;

		public const string WatchdogConfigDescr = "Location of the watchdog configuration";
		public static readonly string WatchdogConfigDefault = string.Empty;

		public const string WatchdogFailureTimeWindowDescr =
			"The time window for which to track supervised node failures.";

		public static readonly int WatchdogFailureTimeWindowDefault = -1;

		public const string WatchdogFailureCountDescr =
			"The maximum allowed supervised node failures within specified time window.";

		public static readonly int WatchdogFailureCountDefault = -1;

		public const string HistogramDescr =
			"Enables the tracking of various histograms in the backend, typically only used for debugging etc";

		public static readonly bool HistogramEnabledDefault = false;

		public const string SkipIndexVerifyDescr =
			"Bypasses the checking of file hashes of indexes during startup and after index merges (allows for faster startup and less disk pressure after merges).";

		public static readonly bool SkipIndexVerifyDefault = false;
		public const string IndexCacheDepthDescr = "Sets the depth to cache for the mid point cache in index.";
		public static int IndexCacheDepthDefault = 16;

		public const string IndexBitnessVersionDescr = "Sets the bitness version for the indexes to use";
		public const byte IndexBitnessVersionDefault = EventStore.Core.Index.PTableVersions.IndexV4;

		public const string OptimizeIndexMergeDescr =
			"Makes index merges faster and reduces disk pressure during merges.";

		public static readonly bool OptimizeIndexMergeDefault = false;

		/*
		 * Authentication Options
		 */
		public const string AuthenticationTypeDescr = "The type of authentication to use.";
		public static readonly string AuthenticationTypeDefault = "internal";

		public const string AuthenticationConfigFileDescr =
			"Path to the configuration file for authentication configuration (if applicable).";

		public static readonly string AuthenticationConfigFileDefault = string.Empty;

		/*
		 * Scavenge options
		 */
		public const string MaxAutoMergeIndexLevelDescr =
			"During large Index Merge operations, writes may be slowed down. Set this to the maximum index file level for which automatic merges should happen.  Merging indexes above this level should be done manually.";

		public static readonly int MaxAutoMergeIndexLevelDefault = int.MaxValue;
	}
}
