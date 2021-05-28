using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Configuration;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Services.Monitoring;
using EventStore.Core.Settings;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Util;
using Microsoft.Extensions.Configuration;

#nullable enable
namespace EventStore.Core {
	public partial record ClusterVNodeOptions {
		public static readonly ClusterVNodeOptions Default = new();

		internal IConfigurationRoot? ConfigurationRoot { get; init; }
		[OptionGroup] public ApplicationOptions Application { get; init; } = new();
		[OptionGroup] public AuthOptions Auth { get; init; } = new();
		[OptionGroup] public CertificateOptions Certificate { get; init; } = new();
		[OptionGroup] public CertificateFileOptions CertificateFile { get; init; } = new();
		[OptionGroup] public CertificateStoreOptions CertificateStore { get; init; } = new();
		[OptionGroup] public ClusterOptions Cluster { get; init; } = new();
		[OptionGroup] public DatabaseOptions Database { get; init; } = new();
		[OptionGroup] public GrpcOptions Grpc { get; init; } = new();
		[OptionGroup] public InterfaceOptions Interface { get; init; } = new();
		[OptionGroup] public ProjectionOptions Projections { get; init; } = new();

		public byte IndexBitnessVersion { get; init; } = Index.PTableVersions.IndexV4;

		public IReadOnlyList<ISubsystem> Subsystems { get; init; } = Array.Empty<ISubsystem>();

		public ClusterVNodeOptions WithSubsystem(ISubsystem subsystem) => this with {
			Subsystems = new List<ISubsystem>(Subsystems) {subsystem}
		};

		public X509Certificate2? ServerCertificate { get; init; }

		public X509Certificate2Collection? TrustedRootCertificates { get; init; }

		internal string? DebugView => ConfigurationRoot?.GetDebugView();

		public static ClusterVNodeOptions FromConfiguration(string[] args, IDictionary environment) {
			if (args == null) throw new ArgumentNullException(nameof(args));
			if (environment == null) throw new ArgumentNullException(nameof(environment));

			var configurationRoot = new ConfigurationBuilder()
				.AddEventStore(args, environment, DefaultValues)
				.Build();

			return FromConfiguration(configurationRoot);
		}

		public static ClusterVNodeOptions FromConfiguration(IConfigurationRoot configurationRoot) {
			configurationRoot.Validate<ClusterVNodeOptions>();

			return new ClusterVNodeOptions {
				Application = ApplicationOptions.FromConfiguration(configurationRoot),
				Auth = AuthOptions.FromConfiguration(configurationRoot),
				Certificate = CertificateOptions.FromConfiguration(configurationRoot),
				CertificateFile = CertificateFileOptions.FromConfiguration(configurationRoot),
				CertificateStore = CertificateStoreOptions.FromConfiguration(configurationRoot),
				Cluster = ClusterOptions.FromConfiguration(configurationRoot),
				Database = DatabaseOptions.FromConfiguration(configurationRoot),
				Grpc = GrpcOptions.FromConfiguration(configurationRoot),
				Interface = InterfaceOptions.FromConfiguration(configurationRoot),
				Projections = ProjectionOptions.FromConfiguration(configurationRoot),
				ConfigurationRoot = configurationRoot,
			};
		}

		public ClusterVNodeOptions Reload() => ConfigurationRoot == null ? this : FromConfiguration(ConfigurationRoot);

		[Description("Application Options")]
		public record ApplicationOptions {
			[Description("Show help.")] public bool Help { get; init; } = false;

			[Description("Show version.")] public bool Version { get; init; } = false;

			[Description("Path where to keep log files.")]
			public string Log { get; init; } = Locations.DefaultLogDirectory;

			[Description("Sets the minimum log level. For more granular settings, please edit logconfig.json.")]
			public LogLevel LogLevel { get; init; } = LogLevel.Default;

			[Description("Configuration files.")]
			public string Config { get; init; } =
				Path.Combine(Locations.DefaultConfigurationDirectory, DefaultFiles.DefaultConfigFile);

			[Description("Print effective configuration to console and then exit.")]
			public bool WhatIf { get; init; } = false;

			[Description("Start the built in system projections.")]
			public bool StartStandardProjections { get; init; } = false;

			[Description("Disable HTTP caching.")] public bool DisableHttpCaching { get; init; } = false;

			[Description("The number of seconds between statistics gathers.")]
			public int StatsPeriodSec { get; init; } = 30;

			[Description("The number of threads to use for pool of worker services.")]
			public int WorkerThreads { get; init; } = 5;

			[Description("Enables the tracking of various histograms in the backend, " +
			             "typically only used for debugging, etc.")]
			public bool EnableHistograms { get; init; } = false;

			[Description("Log Http Requests and Responses before processing them.")]
			public bool LogHttpRequests { get; init; } = false;

			[Description("Log the failed authentication attempts.")]
			public bool LogFailedAuthenticationAttempts { get; init; } = false;

			[Description("Skip Index Scan on Reads. This skips the index scan which was used " +
			             "to stop reading duplicates.")]
			public bool SkipIndexScanOnReads { get; init; } = false;

			[Description("The maximum size of appends, in bytes. May not exceed 16MB.")]
			public int MaxAppendSize { get; init; } = 1_024 * 1_024;

			[Description("Disable Authentication, Authorization and TLS on all TCP/HTTP interfaces.")]
			public bool Insecure { get; init; } = false;

			internal static ApplicationOptions FromConfiguration(IConfigurationRoot configurationRoot) => new() {
				Log = configurationRoot.GetValue<string>(nameof(Log)),
				Config = configurationRoot.GetValue<string>(nameof(Config)),
				Help = configurationRoot.GetValue<bool>(nameof(Help)),
				Version = configurationRoot.GetValue<bool>(nameof(Version)),
				EnableHistograms = configurationRoot.GetValue<bool>(nameof(EnableHistograms)),
				WhatIf = configurationRoot.GetValue<bool>(nameof(WhatIf)),
				WorkerThreads = configurationRoot.GetValue<int>(nameof(WorkerThreads)),
				DisableHttpCaching = configurationRoot.GetValue<bool>(nameof(DisableHttpCaching)),
				LogHttpRequests = configurationRoot.GetValue<bool>(nameof(LogHttpRequests)),
				MaxAppendSize = configurationRoot.GetValue<int>(nameof(MaxAppendSize)),
				StatsPeriodSec = configurationRoot.GetValue<int>(nameof(StatsPeriodSec)),
				StartStandardProjections = configurationRoot.GetValue<bool>(nameof(StartStandardProjections)),
				LogFailedAuthenticationAttempts =
					configurationRoot.GetValue<bool>(nameof(LogFailedAuthenticationAttempts)),
				SkipIndexScanOnReads = configurationRoot.GetValue<bool>(nameof(SkipIndexScanOnReads)),
				Insecure = configurationRoot.GetValue<bool>(nameof(Insecure)),
				LogLevel = configurationRoot.GetValue<LogLevel>(nameof(LogLevel))
			};
		}

		[Description("Authentication/Authorization Options")]
		public record AuthOptions {
			[Description("The type of authorization to use.")]
			public string AuthorizationType { get; init; } = "internal";

			[Description("Path to the configuration file for authorization configuration (if applicable).")]
			public string? AuthorizationConfig { get; init; }

			[Description("The type of Authentication to use.")]
			public string AuthenticationType { get; init; } = "internal";

			[Description("Path to the configuration file for Authentication configuration (if applicable).")]
			public string? AuthenticationConfig { get; init; }

			[Description("Disables first level authorization checks on all HTTP endpoints. " +
			             "This option can be enabled for backwards compatibility with EventStore 5.0.1 or earlier.")]
			public bool DisableFirstLevelHttpAuthorization { get; init; } = false;

			internal static AuthOptions FromConfiguration(IConfigurationRoot configurationRoot) => new() {
				AuthorizationType = configurationRoot.GetValue<string>(nameof(AuthorizationType)),
				AuthenticationConfig = configurationRoot.GetValue<string>(nameof(AuthenticationConfig)),
				AuthenticationType = configurationRoot.GetValue<string>(nameof(AuthenticationType)),
				AuthorizationConfig = configurationRoot.GetValue<string>(nameof(AuthorizationConfig)),
				DisableFirstLevelHttpAuthorization =
					configurationRoot.GetValue<bool>(nameof(DisableFirstLevelHttpAuthorization))
			};
		}

		[Description("Certificate Options (from file)")]
		public record CertificateFileOptions {
			[Description("The path to a PKCS #12 (.p12/.pfx) or an X.509 (.pem, .crt, .cer, .der) certificate file.")]
			public string? CertificateFile { get; init; }

			[Description("The path to the certificate private key file (.key) if an X.509 (.pem, .crt, .cer, .der) " +
			             "certificate file is provided.")]
			public string? CertificatePrivateKeyFile { get; init; }

			[Description("The password to the certificate if a PKCS #12 (.p12/.pfx) certificate file is provided."),
			 Sensitive]
			public string? CertificatePassword { get; init; }

			public static CertificateFileOptions FromConfiguration(IConfigurationRoot configurationRoot) => new() {
				CertificatePassword = configurationRoot.GetValue<string>(nameof(CertificatePassword)),
				CertificatePrivateKeyFile = configurationRoot.GetValue<string>(nameof(CertificatePrivateKeyFile)),
				CertificateFile = configurationRoot.GetValue<string>(nameof(CertificateFile))
			};
		}

		[Description("Certificate Options")]
		public record CertificateOptions {
			[Description("The path to a directory which contains trusted X.509 (.pem, .crt, .cer, .der) " +
			             "root certificate files.")]
			public string? TrustedRootCertificatesPath { get; init; }

			[Description("The reserved common name to authenticate EventStoreDB nodes/servers from certificates")]
			public string CertificateReservedNodeCommonName { get; init; } = "eventstoredb-node";

			internal static CertificateOptions FromConfiguration(IConfigurationRoot configurationRoot) => new() {
				TrustedRootCertificatesPath = configurationRoot.GetValue<string>(nameof(TrustedRootCertificatesPath)),
				CertificateReservedNodeCommonName =
					configurationRoot.GetValue<string>(nameof(CertificateReservedNodeCommonName))
			};
		}

		[Description("Certificate Options (from store)")]
		public record CertificateStoreOptions {
			[Description("The certificate store location name.")]
			public string CertificateStoreLocation { get; init; } = string.Empty;

			[Description("The certificate store location name.")]
			public string CertificateStoreName { get; init; } = string.Empty;

			[Description("The certificate store subject name.")]
			public string CertificateSubjectName { get; init; } = string.Empty;

			[Description("The certificate fingerprint/thumbprint.")]
			public string CertificateThumbprint { get; init; } = string.Empty;

			internal static CertificateStoreOptions FromConfiguration(IConfigurationRoot configurationRoot) => new() {
				CertificateStoreLocation = configurationRoot.GetValue<string>(nameof(CertificateStoreLocation)),
				CertificateStoreName = configurationRoot.GetValue<string>(nameof(CertificateStoreName)),
				CertificateSubjectName = configurationRoot.GetValue<string>(nameof(CertificateSubjectName)),
				CertificateThumbprint = configurationRoot.GetValue<string>(nameof(CertificateThumbprint))
			};
		}

		[Description("Cluster Options")]
		public record ClusterOptions {
			[Description("The maximum number of entries to keep in the stream info cache.")]
			public int StreamInfoCacheCapacity { get; init; } = 100_000;

			[Description("The number of nodes in the cluster.")]
			public int ClusterSize { get; init; } = 1;

			[Description("The node priority used during leader election.")]
			public int NodePriority { get; init; } = 0;

			[Description("The number of nodes which must acknowledge commits before acknowledging to a client.")]
			public int CommitCount { get; init; } = -1;

			[Description("The number of nodes which must acknowledge prepares.")]
			public int PrepareCount { get; init; } = -1;

			[Description("Whether to use DNS lookup to discover other cluster nodes.")]
			public bool DiscoverViaDns { get; init; } = true;

			[Description("DNS name from which other nodes can be discovered.")]
			public string ClusterDns { get; init; } = "fake.dns";

			[Description("The port on which cluster nodes' managers are running.")]
			public int ClusterGossipPort { get; init; } = 2113;

			[Description("Endpoints for other cluster nodes from which to seed gossip.")]
			public EndPoint[] GossipSeed { get; init; } = Array.Empty<EndPoint>();

			[Description("The interval, in ms, nodes should try to gossip with each other.")]
			public int GossipIntervalMs { get; init; } = 2_000;

			[Description("The amount of drift, in ms, between clocks on nodes allowed before gossip is rejected.")]
			public int GossipAllowedDifferenceMs { get; init; } = 60_000;

			[Description("The timeout, in ms, on gossip to another node.")]
			public int GossipTimeoutMs { get; init; } = 2_500;

			[Description("Sets this node as a read only replica that is not allowed to participate in elections " +
			             "or accept writes from clients.")]
			public bool ReadOnlyReplica { get; init; } = false;

			[Description("Allow more nodes than the cluster size to join the cluster as clones. " +
			             "(UNSAFE: can cause data loss if a clone is promoted as leader)")]
			public bool UnsafeAllowSurplusNodes { get; init; } = false;

			[Description("The number of seconds a dead node will remain in the gossip before being pruned.")]
			public int DeadMemberRemovalPeriodSec { get; init; } = 1_800;

			public int QuorumSize => ClusterSize == 1 ? 1 : ClusterSize / 2 + 1;
			public int PrepareAckCount => PrepareCount > QuorumSize ? PrepareCount : QuorumSize;
			public int CommitAckCount => CommitCount > QuorumSize ? CommitCount : QuorumSize;

			internal static ClusterOptions FromConfiguration(IConfigurationRoot configurationRoot) => new() {
				GossipSeed = Array.ConvertAll(configurationRoot.GetCommaSeparatedValueAsArray(nameof(GossipSeed)),
					ParseEndPoint),
				DiscoverViaDns = configurationRoot.GetValue<bool>(nameof(DiscoverViaDns)),
				ClusterSize = configurationRoot.GetValue<int>(nameof(ClusterSize)),
				NodePriority = configurationRoot.GetValue<int>(nameof(NodePriority)),
				CommitCount = configurationRoot.GetValue<int>(nameof(CommitCount)),
				PrepareCount = configurationRoot.GetValue<int>(nameof(PrepareCount)),
				ClusterDns = configurationRoot.GetValue<string>(nameof(ClusterDns)),
				ClusterGossipPort = configurationRoot.GetValue<int>(nameof(ClusterGossipPort)),
				GossipIntervalMs = configurationRoot.GetValue<int>(nameof(GossipIntervalMs)),
				GossipAllowedDifferenceMs = configurationRoot.GetValue<int>(nameof(GossipAllowedDifferenceMs)),
				GossipTimeoutMs = configurationRoot.GetValue<int>(nameof(GossipTimeoutMs)),
				ReadOnlyReplica = configurationRoot.GetValue<bool>(nameof(ReadOnlyReplica)),
				UnsafeAllowSurplusNodes = configurationRoot.GetValue<bool>(nameof(UnsafeAllowSurplusNodes)),
				DeadMemberRemovalPeriodSec = configurationRoot.GetValue<int>(nameof(DeadMemberRemovalPeriodSec)),
				StreamInfoCacheCapacity = configurationRoot.GetValue<int>(nameof(StreamInfoCacheCapacity))
			};
		}

		[Description("Database Options")]
		public record DatabaseOptions {
			[Description("The minimum flush delay in milliseconds.")]
			public double MinFlushDelayMs { get; init; } = TFConsts.MinFlushDelayMs.TotalMilliseconds;

			[Description("Disables the merging of chunks when scavenge is running.")]
			public bool DisableScavengeMerging { get; init; } = false;

			[Description("The number of days to keep scavenge history.")]
			public int ScavengeHistoryMaxAge { get; init; } = 30;

			[Description("The number of chunks to cache in unmanaged memory.")]
			public int CachedChunks { get; init; } = -1;

			[Description("The amount of unmanaged memory to use for caching chunks in bytes.")]
			public long ChunksCacheSize { get; init; } = TFConsts.ChunksCacheSize;

			[Description("Adjusts the maximum size of a mem table.")]
			public int MaxMemTableSize { get; init; } = 1_000_000;

			[Description("The number of events to read per candidate in the case of a hash collision.")]
			public int HashCollisionReadLimit { get; init; } = 100;

			[Description("The path the db should be loaded/saved to.")]
			public string Db { get; init; } = Locations.DefaultDataDirectory;

			[Description("The path the index should be loaded/saved to.")]
			public string? Index { get; init; } = null;

			[Description("Keep everything in memory, no directories or files are created.")]
			public bool MemDb { get; init; } = false;

			[Description("Bypasses the checking of file hashes of database during startup " +
			             "(allows for faster startup).")]
			public bool SkipDbVerify { get; init; } = false;

			[Description("Enables Write Through when writing to the file system, this bypasses filesystem caches.")]
			public bool WriteThrough { get; init; } = false;

			[Description("Enables Unbuffered/DirectIO when writing to the file system, this bypasses filesystem " +
			             "caches.")]
			public bool Unbuffered { get; init; } = false;

			[Description("The initial number of readers to start when opening a TFChunk.")]
			public int ChunkInitialReaderCount { get; init; } = 5;

			[Description("Prepare timeout (in milliseconds).")]
			public int PrepareTimeoutMs { get; init; } = 2_000;

			[Description("Commit timeout (in milliseconds).")]
			public int CommitTimeoutMs { get; init; } = 2_000;

			[Description("Write timeout (in milliseconds).")]
			public int WriteTimeoutMs { get; init; } = 2_000;

			private readonly bool _unsafeDisableFlushToDisk = false;

			[Description("Disable flushing to disk. (UNSAFE: on power off)")]
			public bool UnsafeDisableFlushToDisk {
				get => _unsafeDisableFlushToDisk;
				init {
					_unsafeDisableFlushToDisk = value;
					FileStreamExtensions.ConfigureFlush(value);
				}
			}

			[Description("Disables Hard Deletes. (UNSAFE: use to remove hard deletes)")]
			public bool UnsafeIgnoreHardDelete { get; init; } = false;

			[Description("Bypasses the checking of file hashes of indexes during startup and after index merges " +
			             "(allows for faster startup and less disk pressure after merges).")]
			public bool SkipIndexVerify { get; init; } = false;

			[Description("Sets the depth to cache for the mid point cache in index.")]
			public int IndexCacheDepth { get; init; } = 16;

			[Description("Makes index merges faster and reduces disk pressure during merges.")]
			public bool OptimizeIndexMerge { get; init; } = false;

			[Description("Always keeps the newer chunks from a scavenge operation.")]
			public bool AlwaysKeepScavenged { get; init; } = false;

			[Description("Change the way the DB files are opened to reduce their stickiness in the system file cache.")]
			public bool ReduceFileCachePressure { get; init; } = false;

			[Description("Number of threads to be used to initialize the database. " +
			             "Will be capped at host processor count.")]
			public int InitializationThreads { get; init; } = 1;

			[Description("The number of reader threads to use for processing reads.")]
			public int ReaderThreadsCount { get; init; } = 4;

			[Description("During large Index Merge operations, writes may be slowed down. Set this to the maximum " +
			             "index file level for which automatic merges should happen. Merging indexes above this level " +
			             "should be done manually.")]
			public int MaxAutoMergeIndexLevel { get; init; } = int.MaxValue;

			[Description("Set this option to write statistics to the database.")]
			public bool WriteStatsToDb {
				get => (StatsStorage.Stream & StatsStorage) != 0;
				init => StatsStorage = value ? StatsStorage.StreamAndFile : StatsStorage.File;
			}

			[Description("When truncate.chk is set, the database will be truncated on startup. " +
			             "This is a safety check to ensure large amounts of data truncation does not happen " +
			             "accidentally. This value should be set in the low 10,000s for allow for " +
			             "standard cluster recovery operations. -1 is no max.")]
			public long MaxTruncation { get; init; } = 256 * 1_024 * 1_024;

			public int ChunkSize { get; init; } = TFConsts.ChunkSize;

			public StatsStorage StatsStorage { get; init; } = StatsStorage.File;

			[Description("The log format version to use for storing the event log. " +
			             "V3 is currently in development and should only be used for testing purposes.")]
			public DbLogFormat DbLogFormat { get; init; } = DbLogFormat.V2;
			
			public int GetPTableMaxReaderCount() {
				var ptableMaxReaderCount = 1 /* StorageWriter */
				                           + 1 /* StorageChaser */
				                           + 1 /* Projections */
				                           + TFChunkScavenger.MaxThreadCount /* Scavenging (1 per thread) */
				                           + 1 /* Subscription LinkTos resolving */
				                           + ReaderThreadsCount
				                           + 5 /* just in case reserve :) */;
				return Math.Max(ptableMaxReaderCount, ESConsts.PTableInitialReaderCount);
			}


			internal int GetTFChunkMaxReaderCount() {
				var tfChunkMaxReaderCount =
					GetPTableMaxReaderCount() +
					2 + /* for caching/uncaching, populating midpoints */
					1 + /* for epoch manager usage of elections/replica service */
					1 /* for epoch manager usage of leader replication service */;
				return Math.Max(tfChunkMaxReaderCount, ChunkInitialReaderCount);
			}


			internal static DatabaseOptions FromConfiguration(IConfigurationRoot configurationRoot) => new() {
				MinFlushDelayMs = configurationRoot.GetValue<double>(nameof(MinFlushDelayMs)),
				DisableScavengeMerging = configurationRoot.GetValue<bool>(nameof(DisableScavengeMerging)),
				MemDb = configurationRoot.GetValue<bool>(nameof(MemDb)),
				Db = configurationRoot.GetValue<string>(nameof(Db)),
				ScavengeHistoryMaxAge = configurationRoot.GetValue<int>(nameof(ScavengeHistoryMaxAge)),
				CachedChunks = configurationRoot.GetValue<int>(nameof(CachedChunks)),
				ChunksCacheSize = configurationRoot.GetValue<long>(nameof(ChunksCacheSize)),
				MaxMemTableSize = configurationRoot.GetValue<int>(nameof(MaxMemTableSize)),
				HashCollisionReadLimit = configurationRoot.GetValue<int>(nameof(HashCollisionReadLimit)),
				Index = configurationRoot.GetValue<string?>(nameof(Index)),
				SkipDbVerify = configurationRoot.GetValue<bool>(nameof(SkipDbVerify)),
				WriteThrough = configurationRoot.GetValue<bool>(nameof(WriteThrough)),
				Unbuffered = configurationRoot.GetValue<bool>(nameof(Unbuffered)),
				ChunkInitialReaderCount = configurationRoot.GetValue<int>(nameof(ChunkInitialReaderCount)),
				PrepareTimeoutMs = configurationRoot.GetValue<int>(nameof(PrepareTimeoutMs)),
				CommitTimeoutMs = configurationRoot.GetValue<int>(nameof(CommitTimeoutMs)),
				WriteTimeoutMs = configurationRoot.GetValue<int>(nameof(WriteTimeoutMs)),
				UnsafeDisableFlushToDisk = configurationRoot.GetValue<bool>(nameof(UnsafeDisableFlushToDisk)),
				UnsafeIgnoreHardDelete = configurationRoot.GetValue<bool>(nameof(UnsafeIgnoreHardDelete)),
				SkipIndexVerify = configurationRoot.GetValue<bool>(nameof(SkipIndexVerify)),
				IndexCacheDepth = configurationRoot.GetValue<int>(nameof(IndexCacheDepth)),
				OptimizeIndexMerge = configurationRoot.GetValue<bool>(nameof(OptimizeIndexMerge)),
				AlwaysKeepScavenged = configurationRoot.GetValue<bool>(nameof(AlwaysKeepScavenged)),
				ReduceFileCachePressure = configurationRoot.GetValue<bool>(nameof(ReduceFileCachePressure)),
				InitializationThreads = configurationRoot.GetValue<int>(nameof(InitializationThreads)),
				ReaderThreadsCount = configurationRoot.GetValue<int>(nameof(ReaderThreadsCount)),
				MaxAutoMergeIndexLevel = configurationRoot.GetValue<int>(nameof(MaxAutoMergeIndexLevel)),
				WriteStatsToDb = configurationRoot.GetValue<bool>(nameof(WriteStatsToDb)),
				MaxTruncation = configurationRoot.GetValue<long>(nameof(MaxTruncation)),
				DbLogFormat = configurationRoot.GetValue<DbLogFormat>(nameof(DbLogFormat))
			};
		}
		
		[Description("gRPC Options")]
		public record GrpcOptions {
			[Description("Controls the period (in milliseconds) after which a keepalive ping " +
			             "is sent on the transport.")]
			public int KeepAliveInterval { get; init; } = 10_000;

			[Description("Controls the amount of time (in milliseconds) the sender of the keepalive ping waits " +
			             "for an acknowledgement. If it does not receive an acknowledgment within this time, " +
			             "it will close the connection.")]
			public int KeepAliveTimeout { get; init; } = 10_000;

			internal static GrpcOptions FromConfiguration(IConfigurationRoot configurationRoot) => new() {
				KeepAliveInterval = configurationRoot.GetValue<int>(nameof(KeepAliveInterval)),
				KeepAliveTimeout = configurationRoot.GetValue<int>(nameof(KeepAliveTimeout))
			};
		}

		[Description("Interface Options")]
		public record InterfaceOptions {
			[Description("Internal IP Address.")] public IPAddress IntIp { get; init; } = IPAddress.Loopback;

			[Description("External IP Address.")] public IPAddress ExtIp { get; init; } = IPAddress.Loopback;

			[Description("The port to run the HTTP server on.")]
			public int HttpPort { get; init; } = 2113;

			[Description("Whether to enable external TCP communication"),
			 Deprecated(
				 "The Legacy TCP Client Interface has been deprecated as of version 20.6.0. It is recommended to use gRPC instead.")]
			public bool EnableExternalTcp { get; init; } = false;

			[Description("Internal TCP Port.")] public int IntTcpPort { get; init; } = 1112;

			[Description("External TCP Port.")] public int ExtTcpPort { get; init; } = 1113;

			[Description("Advertise External Tcp Address As.")]
			public string? ExtHostAdvertiseAs { get; init; } = null;

			[Description("Advertise Internal Tcp Address As.")]
			public string? IntHostAdvertiseAs { get; init; } = null;

			[Description("Advertise Host in Gossip to Client As.")]
			public string? AdvertiseHostToClientAs { get; init; } = null;

			[Description("Advertise HTTP Port in Gossip to Client As.")]
			public int AdvertiseHttpPortToClientAs { get; init; } = 0;

			[Description("Advertise TCP Port in Gossip to Client As.")]
			public int AdvertiseTcpPortToClientAs { get; init; } = 0;

			[Description("Advertise External Tcp Port As.")]
			public int ExtTcpPortAdvertiseAs { get; init; } = 0;

			[Description("Advertise Http Port As.")]
			public int HttpPortAdvertiseAs { get; init; } = 0;

			[Description("Advertise Internal Tcp Port As.")]
			public int IntTcpPortAdvertiseAs { get; init; } = 0;

			[Description("Heartbeat timeout for internal TCP sockets.")]
			public int IntTcpHeartbeatTimeout { get; init; } = 700;

			[Description("Heartbeat timeout for external TCP sockets.")]
			public int ExtTcpHeartbeatTimeout { get; init; } = 1_000;

			[Description("Heartbeat interval for internal TCP sockets.")]
			public int IntTcpHeartbeatInterval { get; init; } = 700;

			[Description("Heartbeat interval for external TCP sockets.")]
			public int ExtTcpHeartbeatInterval { get; init; } = 2_000;

			[Description("When enabled, tells a single node to run gossip as if it is a cluster."),
			 Deprecated("The '" + nameof(GossipOnSingleNode) + "' option has been deprecated as of version 21.2.")]
			public bool? GossipOnSingleNode { get; init; } = null;

			[Description("The maximum number of pending send bytes allowed before a connection is closed.")]
			public int ConnectionPendingSendBytesThreshold { get; init; } = 10 * 1_024 * 1_024;

			[Description("The maximum number of pending connection operations allowed before a connection is closed.")]
			public int ConnectionQueueSizeThreshold { get; init; } = 50_000;

			[Description("Disables the admin ui on the HTTP endpoint.")]
			public bool DisableAdminUi { get; init; } = false;

			[Description("Disables statistics requests on the HTTP endpoint.")]
			public bool DisableStatsOnHttp { get; init; } = false;

			[Description("Disables gossip requests on the HTTP endpoint.")]
			public bool DisableGossipOnHttp { get; init; } = false;

			[Description("Enables trusted authentication by an intermediary in the HTTP.")]
			public bool EnableTrustedAuth { get; init; } = false;

			[Description("Whether to disable secure internal TCP communication."),
			 Deprecated("The '" + nameof(DisableInternalTcpTls) +
			            "' option has been deprecated as of version 20.6.1 and currently has no effect. Please use the '" +
			            nameof(Application.Insecure) + "' option instead.")]
			public bool DisableInternalTcpTls { get; init; } = false;

			[Description("Whether to disable secure external TCP communication."),
			Deprecated("The '" + nameof(DisableExternalTcpTls) + "' option has been deprecated as of version 20.6.1.")]
			public bool DisableExternalTcpTls { get; init; } = false;

			[Description("Enable AtomPub over HTTP Interface."),
			 Deprecated(
				 "AtomPub over HTTP Interface has been deprecated as of version 20.6.0. It is recommended to use gRPC instead")]
			public bool EnableAtomPubOverHttp { get; init; } = false;

			internal static InterfaceOptions FromConfiguration(IConfigurationRoot configurationRoot) => new() {
				GossipOnSingleNode = configurationRoot.GetValue<bool?>(nameof(GossipOnSingleNode)),
				IntIp = IPAddress.Parse(configurationRoot.GetValue<string>(nameof(IntIp)) ??
				                        IPAddress.Loopback.ToString()),
				ExtIp = IPAddress.Parse(configurationRoot.GetValue<string>(nameof(ExtIp)) ??
				                        IPAddress.Loopback.ToString()),
				HttpPort = configurationRoot.GetValue<int>(nameof(HttpPort)),
				EnableExternalTcp = configurationRoot.GetValue<bool>(nameof(EnableExternalTcp)),
				ExtTcpPort = configurationRoot.GetValue<int>(nameof(ExtTcpPort)),
				IntTcpPort = configurationRoot.GetValue<int>(nameof(IntTcpPort)),
				ExtHostAdvertiseAs = configurationRoot.GetValue<string?>(nameof(ExtHostAdvertiseAs)),
				AdvertiseHostToClientAs = configurationRoot.GetValue<string?>(nameof(AdvertiseHostToClientAs)),
				AdvertiseHttpPortToClientAs = configurationRoot.GetValue<int>(nameof(AdvertiseHttpPortToClientAs)),
				AdvertiseTcpPortToClientAs = configurationRoot.GetValue<int>(nameof(AdvertiseTcpPortToClientAs)),
				ExtTcpPortAdvertiseAs = configurationRoot.GetValue<int>(nameof(ExtTcpPortAdvertiseAs)),
				IntHostAdvertiseAs = configurationRoot.GetValue<string>(nameof(IntHostAdvertiseAs)),
				HttpPortAdvertiseAs = configurationRoot.GetValue<int>(nameof(HttpPortAdvertiseAs)),
				IntTcpPortAdvertiseAs = configurationRoot.GetValue<int>(nameof(IntTcpPortAdvertiseAs)),
				ExtTcpHeartbeatTimeout = configurationRoot.GetValue<int>(nameof(ExtTcpHeartbeatTimeout)),
				IntTcpHeartbeatTimeout = configurationRoot.GetValue<int>(nameof(IntTcpHeartbeatTimeout)),
				ExtTcpHeartbeatInterval = configurationRoot.GetValue<int>(nameof(ExtTcpHeartbeatInterval)),
				IntTcpHeartbeatInterval = configurationRoot.GetValue<int>(nameof(IntTcpHeartbeatInterval)),
				ConnectionPendingSendBytesThreshold =
					configurationRoot.GetValue<int>(nameof(ConnectionPendingSendBytesThreshold)),
				ConnectionQueueSizeThreshold = configurationRoot.GetValue<int>(nameof(ConnectionQueueSizeThreshold)),
				DisableAdminUi = configurationRoot.GetValue<bool>(nameof(DisableAdminUi)),
				DisableStatsOnHttp = configurationRoot.GetValue<bool>(nameof(DisableStatsOnHttp)),
				DisableGossipOnHttp = configurationRoot.GetValue<bool>(nameof(DisableGossipOnHttp)),
				EnableTrustedAuth = configurationRoot.GetValue<bool>(nameof(EnableTrustedAuth)),
				DisableInternalTcpTls = configurationRoot.GetValue<bool>(nameof(DisableInternalTcpTls)),
				DisableExternalTcpTls = configurationRoot.GetValue<bool>(nameof(DisableExternalTcpTls)),
				EnableAtomPubOverHttp = configurationRoot.GetValue<bool>(nameof(EnableAtomPubOverHttp))
			};
		}

		[Description("Projection Options")]
		public record ProjectionOptions {
			[Description("Enables the running of projections. System runs built-in projections, " +
			             "All runs user projections.")]
			public ProjectionType RunProjections { get; init; }

			[Description("The number of threads to use for projections.")]
			public int ProjectionThreads { get; init; } = 3;

			[Description("The number of minutes a query can be idle before it expires.")]
			public int ProjectionsQueryExpiry { get; init; } = 5;

			[Description("Fault the projection if the Event number that was expected in the stream differs " +
			             "from what is received. This may happen if events have been deleted or expired.")]
			public bool FaultOutOfOrderProjections { get; init; } = false;

			internal static ProjectionOptions FromConfiguration(IConfigurationRoot configurationRoot) => new() {
				RunProjections = configurationRoot.GetValue<ProjectionType>(nameof(RunProjections)),
				ProjectionThreads = configurationRoot.GetValue<int>(nameof(ProjectionThreads)),
				ProjectionsQueryExpiry = configurationRoot.GetValue<int>(nameof(ProjectionsQueryExpiry)),
				FaultOutOfOrderProjections = configurationRoot.GetValue<bool>(nameof(FaultOutOfOrderProjections))
			};
		}
	}
}
