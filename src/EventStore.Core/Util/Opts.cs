﻿using System;
using System.Net;
using EventStore.Common.Options;
using EventStore.Core.TransactionLogV2.Chunks;

namespace EventStore.Core.Util {
	public static class Opts {
		public const string EnvPrefix = "EVENTSTORE_";

		/*
		 * OPTIONS GROUPS
		 */
		public const string AppGroup = "Application Options";
		public const string DbGroup = "Database Options";
		public const string ProjectionsGroup = "Projections Options";
		public const string AuthGroup = "Authentication/Authorization Options";
		public const string InterfacesGroup = "Interface Options";
		public const string CertificateGroup = "Certificate Options";
		public const string CertificatesFromFileGroup = "Certificate Options (from file)";
		public const string CertificatesFromStoreGroup = "Certificate Options (from store)";
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
		public const string LogLevelDescr = "Sets the minimum log level. For more granular settings, please edit logconfig.json.";
		public const LogLevel LogLevelDefault = LogLevel.Default;

		public const string ConfigsDescr = "Configuration files.";
		public static readonly string[] ConfigsDefault = new string[0];

		public const string ShowHelpDescr = "Show help.";
		public const bool ShowHelpDefault = false;

		public const string ShowVersionDescr = "Show version.";
		public const bool ShowVersionDefault = false;

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
		
		public const string ConnectionQueueSizeThresholdDescr =
			"The maximum number of pending connection operations allowed before a connection is closed.";

		public const int ConnectionQueueSizeThresholdDefault = 50000;

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

		public const string NodePriorityDescr = "The node priority used during leader election";
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

		public const string WriteTimeoutMsDescr = "Write timeout (in milliseconds).";
		public static readonly int WriteTimeoutMsDefault = 2000; // 2 seconds

		public const string LogHttpRequestsDescr = "Log Http Requests and Responses before processing them.";
		public static readonly bool LogHttpRequestsDefault = false;
		
		public const string LogFailedAuthenticationAttemptsDescr = "Log the failed authentication attempts.";
		public static readonly bool LogFailedAuthenticationAttemptsDefault = false;

		public const string SkipIndexScanOnReadsDescr =
			"Skip Index Scan on Reads. This skips the index scan which was used to stop reading duplicates.";

		public static readonly bool SkipIndexScanOnReadsDefault = false;

		public const string ReduceFileCachePressureDescr =
			"Change the way the DB files are opened to reduce their stickiness in the system file cache.";

		public static readonly bool ReduceFileCachePressureDefault = false;

		public const string InitializationThreadsDescr =
			"Number of threads to be used to initialize the database. Will be capped at host processor count.";

		public static readonly int InitializationThreadsDefault = 1;

		//Common certificate options
		public const string TrustedRootCertificatesPathDescr = "The path to a directory which contains trusted X.509 (.pem, .crt, .cer, .der) root certificate files.";
		public static readonly string TrustedRootCertificatesPathDefault = string.Empty;

		//Loading certificates from files
		public const string CertificateFileDescr = "The path to a PKCS #12 (.p12/.pfx) or an X.509 (.pem, .crt, .cer, .der) certificate file.";
		public static readonly string CertificateFileDefault = string.Empty;
		
		public const string CertificatePrivateKeyFileDescr = "The path to the certificate private key file (.key) if an X.509 (.pem, .crt, .cer, .der) certificate file is provided.";
		public static readonly string CertificatePrivateKeyFileDefault = string.Empty;

		public const string CertificatePasswordDescr = "The password to the certificate if a PKCS #12 (.p12/.pfx) certificate file is provided.";
		public static readonly string CertificatePasswordDefault = string.Empty;

		//Loading certificates from a certificate store
		public const string CertificateStoreLocationDescr = "The certificate store location name.";
		public static readonly string CertificateStoreLocationDefault = string.Empty;

		public const string CertificateStoreNameDescr = "The certificate store name.";
		public static readonly string CertificateStoreNameDefault = string.Empty;

		public const string CertificateSubjectNameDescr = "The certificate subject name.";
		public static readonly string CertificateSubjectNameDefault = string.Empty;
		
		public const string CertificateReservedNodeCommonNameDescr = "The reserved common name to authenticate EventStoreDB nodes/servers from certificates";
		public static readonly string CertificateReservedNodeCommonNameDefault = "eventstoredb-node";

		public const string MaxAppendSizeDecr = "The maximum size of appends, in bytes. May not exceed 16MB.";
		public const int MaxAppendSizeDefault = 1024 * 1024; // ONE MB

		public const string CertificateThumbprintDescr = "The certificate fingerprint/thumbprint.";
		public static readonly string CertificateThumbprintDefault = string.Empty;
		
		public const string EnableAtomPubOverHTTPDescr =
			"Enable AtomPub over HTTP Interface.";

		public static readonly bool EnableAtomPubOverHTTPDefault = false;

		/*
		 *  SINGLE NODE OPTIONS
		 */
		public const string HostDescr = "The Host to bind to.";
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
		public const int GossipIntervalMsDefault = 2000;

		public const string GossipTimeoutMsDescr = "The timeout, in ms, on gossip to another node.";
		public const int GossipTimeoutMsDefault = 2500;

		public const string DisableAdminUiDescr = "Disables the admin ui on the HTTP endpoint";
		public const bool DisableAdminUiDefault = false;

		public const string DisableGossipOnHttpDescr = "Disables gossip requests on the HTTP endpoint";
		public const bool DisableGossipOnHttpDefault = false;

		public const string DisableStatsOnHttpDescr = "Disables statistics requests on the HTTP endpoint";
		public const bool DisableStatsOnHttpDefault = false;

		public const string InternalIpDescr = "Internal IP Address.";
		public static readonly IPAddress InternalIpDefault = IPAddress.Loopback;

		public const string ExternalIpDescr = "External IP Address.";
		public static readonly IPAddress ExternalIpDefault = IPAddress.Loopback;

		public const string InternalTcpPortDescr = "Internal TCP Port.";
		public const int InternalTcpPortDefault = 1112;

		public const string ExternalTcpPortDescr = "External TCP Port.";
		public const int ExternalTcpPortDefault = 1113;

		public const string ExternalIpAdvertiseAsDescr = "Advertise External Tcp Address As.";
		public static readonly string ExternalHostAdvertiseAsDefault = null;

		public const string AdvertiseHostToClientAsDescr = "Advertise Host in Gossip to Client As.";
		public static readonly string AdvertiseHostToClientAsDefault  = null;

		public const string AdvertiseHttpPortToClientAsDescr = "Advertise HTTP Port in Gossip to Client As.";
		public static readonly int AdvertiseHttpPortToClientAsDefault  = 0;

		public const string AdvertiseTcpPortToClientAsDescr = "Advertise TCP Port in Gossip to Client As.";
		public static readonly int AdvertiseTcpPortToClientAsDefault  = 0;

		public const string ExternalTcpPortAdvertiseAsDescr = "Advertise External Tcp Port As.";
		public static readonly int ExternalTcpPortAdvertiseAsDefault = 0;

		public const string HttpPortAdvertiseAsDescr = "Advertise Http Port As.";
		public static readonly int HttpPortAdvertiseAsDefault = 0;

		public const string InternalIpAdvertiseAsDescr = "Advertise Internal Tcp Address As.";
		public static readonly string InternalHostAdvertiseAsDefault = null;

		public const string InternalTcpPortAdvertiseAsDescr = "Advertise Internal Tcp Port As.";
		public static readonly int InternalTcpPortAdvertiseAsDefault = 0;

		public const string ClusterSizeDescr = "The number of nodes in the cluster.";
		public const int ClusterSizeDefault = 1;

		public const string CommitCountDescr =
			"The number of nodes which must acknowledge commits before acknowledging to a client.";

		public const int CommitCountDefault = -1;

		public const string PrepareCountDescr = "The number of nodes which must acknowledge prepares.";
		public const int PrepareCountDefault = -1;

		public const string DisableInternalTcpTlsDescr = "Whether to disable secure internal TCP communication.";
		public const bool DisableInternalTcpTlsDefault = false;
		
		public const string DisableExternalTcpTlsDescr = "Whether to disable secure external TCP communication.";
		public const bool DisableExternalTcpTlsDefault = false;

		public const string EnableExternalTCPDescr = "Whether to enable external TCP communication";
		public const bool EnableExternalTCPDefault = false;

		public const string DiscoverViaDnsDescr = "Whether to use DNS lookup to discover other cluster nodes.";
		public const bool DiscoverViaDnsDefault = true;

		public const string ClusterDnsDescr = "DNS name from which other nodes can be discovered.";
		public const string ClusterDnsDefault = "fake.dns";

		public const int ClusterGossipPortDefault = 2113;
		public const string ClusterGossipPortDescr = "The port on which cluster nodes' managers are running.";

		public const string GossipSeedDescr = "Endpoints for other cluster nodes from which to seed gossip";
		public static readonly EndPoint[] GossipSeedDefault = Array.Empty<EndPoint>();

		public const string ReadOnlyReplicaDescr = 
			"Sets this node as a read only replica that is not allowed to participate in elections or accept writes from clients.";
		public static readonly bool ReadOnlyReplicaDefault = false;

		public const string UnsafeAllowSurplusNodesDescr = "Allow more nodes than the cluster size to join the cluster as clones. (UNSAFE: can cause data loss if a clone is promoted as leader)";
		public static readonly bool UnsafeAllowSurplusNodesDefault = false;
		
		public const string DeadMemberRemovalPeriodDescr = "The number of seconds a dead node will remain in the gossip before being pruned";
		public const int DeadMemberRemovalPeriodDefault = 1800;

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
		 * Authentication/Authorization Options
		 */
		public const string AuthorizationTypeDescr = "The type of authorization to use.";
		public static readonly string AuthorizationTypeDefault = "internal";
		
		public const string AuthenticationTypeDescr = "The type of authentication to use.";
		public static readonly string AuthenticationTypeDefault = "internal";
		
		public const string AuthorizationConfigFileDescr =
			"Path to the configuration file for authorization configuration (if applicable).";
		
		public static readonly string AuthorizationConfigFileDefault = string.Empty;

		public const string AuthenticationConfigFileDescr =
			"Path to the configuration file for authentication configuration (if applicable).";

		public static readonly string AuthenticationConfigFileDefault = string.Empty;

		public const string DisableFirstLevelHttpAuthorizationDescr = "Disables first level authorization checks on all HTTP endpoints. This option can be enabled for backwards compatibility with EventStore 5.0.1 or earlier.";
		public static readonly bool DisableFirstLevelHttpAuthorizationDefault = false;

		/*
		 * Scavenge options
		 */
		public const string MaxAutoMergeIndexLevelDescr =
			"During large Index Merge operations, writes may be slowed down. Set this to the maximum index file level for which automatic merges should happen.  Merging indexes above this level should be done manually.";

		public static readonly int MaxAutoMergeIndexLevelDefault = int.MaxValue;

		public const string WriteStatsToDbDescr = "Set this option to write statistics to the database.";
		public const bool WriteStatsToDbDefault = false;
		
		public const string MaxTruncationDescr =
			"When truncate.chk is set, the database will be truncated on startup. This is a safety check to ensure large amounts of data truncation does not happen accidentally. This value should be set in the low 10,000s for allow for standard cluster recovery operations. -1 is no max.";
		public static readonly long MaxTruncationDefault = 256 * 1024 * 1024;
		
		public const string InsecureDescr = "Disable Authentication, Authorization and TLS on all TCP/HTTP interfaces";
		public const bool InsecureDefault = false;
	}
}
