using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Utils;
using EventStore.Core.Authentication;
using EventStore.Core.Authorization;
using EventStore.Core.Data;
using EventStore.Core.Services.Monitoring;
using EventStore.Core.Services.PersistentSubscription.ConsumerStrategy;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Cluster.Settings {
	public class ClusterVNodeSettings {
		public readonly Func<ClusterNodeOptions> LoadConfigFunc;
		public readonly VNodeInfo NodeInfo;
		public readonly GossipAdvertiseInfo GossipAdvertiseInfo;
		public readonly bool EnableTrustedAuth;
		public X509Certificate2 Certificate;
		public X509Certificate2Collection TrustedRootCerts;
		public readonly string CertificateReservedNodeCommonName;
		public readonly int WorkerThreads;
		public readonly bool StartStandardProjections;
		public readonly bool EnableAtomPubOverHTTP;
		public readonly bool DisableHTTPCaching;
		public readonly bool LogHttpRequests;
		public readonly bool LogFailedAuthenticationAttempts;
		public readonly int MaxAppendSize;

		public readonly bool DiscoverViaDns;
		public readonly string ClusterDns;
		public readonly EndPoint[] GossipSeeds;
		public readonly bool EnableHistograms;
		public readonly TimeSpan MinFlushDelay;

		public readonly int ClusterNodeCount;
		public readonly int PrepareAckCount;
		public readonly int CommitAckCount;
		public readonly TimeSpan PrepareTimeout;
		public readonly TimeSpan CommitTimeout;
		public readonly TimeSpan WriteTimeout;

		public readonly int NodePriority;

		public readonly bool DisableInternalTcpTls;
		public readonly bool DisableExternalTcpTls;
		public readonly bool DisableHttps;
		public readonly bool EnableExternalTCP;

		public readonly TimeSpan StatsPeriod;
		public readonly StatsStorage StatsStorage;

		public readonly AuthenticationProviderFactory AuthenticationProviderFactory;
		public readonly AuthorizationProviderFactory AuthorizationProviderFactory;

		public readonly bool DisableFirstLevelHttpAuthorization;
		public readonly bool DisableScavengeMerging;
		public readonly int ScavengeHistoryMaxAge;
		public bool AdminOnPublic;
		public bool StatsOnPublic;
		public bool GossipOnPublic;
		public readonly TimeSpan GossipInterval;
		public readonly TimeSpan GossipAllowedTimeDifference;
		public readonly TimeSpan GossipTimeout;
		public readonly TimeSpan IntTcpHeartbeatTimeout;
		public readonly TimeSpan IntTcpHeartbeatInterval;
		public readonly TimeSpan ExtTcpHeartbeatTimeout;
		public readonly TimeSpan ExtTcpHeartbeatInterval;
		public readonly TimeSpan DeadMemberRemovalPeriod;

		public readonly int ConnectionPendingSendBytesThreshold;
		public readonly int ConnectionQueueSizeThreshold;
		public readonly bool UnsafeIgnoreHardDeletes;
		public readonly bool VerifyDbHash;
		public readonly int MaxMemtableEntryCount;
		public readonly int HashCollisionReadLimit;
		public readonly bool SkipIndexVerify;
		public readonly int IndexCacheDepth;
		public readonly byte IndexBitnessVersion;
		public readonly bool OptimizeIndexMerge;

		public readonly string Index;
		public readonly int ReaderThreadsCount;
		public readonly IPersistentSubscriptionConsumerStrategyFactory[] AdditionalConsumerStrategies;
		public readonly bool AlwaysKeepScavenged;
		public readonly bool SkipIndexScanOnReads;
		public readonly bool ReduceFileCachePressure;
		public readonly int InitializationThreads;
		public readonly int MaxAutoMergeIndexLevel;
		public readonly long MaxTruncation;

		public readonly bool GossipOnSingleNode;
		public readonly bool FaultOutOfOrderProjections;
		public readonly bool ReadOnlyReplica;
		public int PTableMaxReaderCount;
		public readonly bool UnsafeAllowSurplusNodes;

		public ClusterVNodeSettings(Guid instanceId, int debugIndex,
			Func<ClusterNodeOptions> loadConfigFunc,
			IPEndPoint internalTcpEndPoint,
			IPEndPoint internalSecureTcpEndPoint,
			IPEndPoint externalTcpEndPoint,
			IPEndPoint externalSecureTcpEndPoint,
			IPEndPoint httpEndPoint,
			GossipAdvertiseInfo gossipAdvertiseInfo,
			bool enableTrustedAuth,
			X509Certificate2 certificate,
			X509Certificate2Collection trustedRootCerts,
			string certificateReservedNodeCommonName,
			int workerThreads,
			bool discoverViaDns,
			string clusterDns,
			EndPoint[] gossipSeeds,
			TimeSpan minFlushDelay,
			int clusterNodeCount,
			int prepareAckCount,
			int commitAckCount,
			TimeSpan prepareTimeout,
			TimeSpan commitTimeout,
			TimeSpan writeTimeout,
			bool disableInternalTcpTls,
			bool disableExternalTcpTls,
			TimeSpan statsPeriod,
			StatsStorage statsStorage,
			int nodePriority,
			AuthenticationProviderFactory authenticationProviderFactory,
			AuthorizationProviderFactory authorizationProviderFactory,
			bool disableScavengeMerging,
			int scavengeHistoryMaxAge,
			bool adminOnPublic,
			bool statsOnPublic,
			bool gossipOnPublic,
			TimeSpan gossipInterval,
			TimeSpan gossipAllowedTimeDifference,
			TimeSpan gossipTimeout,
			TimeSpan intTcpHeartbeatTimeout,
			TimeSpan intTcpHeartbeatInterval,
			TimeSpan extTcpHeartbeatTimeout,
			TimeSpan extTcpHeartbeatInterval,
			TimeSpan deadMemberRemovalPeriod,
			bool verifyDbHash,
			int maxMemtableEntryCount,
			int hashCollisionReadLimit,
			bool startStandardProjections,
			bool disableHTTPCaching,
			bool logHttpRequests,
			int connectionPendingSendBytesThreshold,
			int connectionQueueSizeThreshold,
			int ptableMaxReaderCount,
			string index = null, bool enableHistograms = false,
			bool skipIndexVerify = false,
			int indexCacheDepth = 16,
			byte indexBitnessVersion = 4,
			bool optimizeIndexMerge = false,
			IPersistentSubscriptionConsumerStrategyFactory[] additionalConsumerStrategies = null,
			bool unsafeIgnoreHardDeletes = false,
			int readerThreadsCount = 4,
			bool alwaysKeepScavenged = false,
			bool gossipOnSingleNode = false,
			bool skipIndexScanOnReads = false,
			bool reduceFileCachePressure = false,
			int initializationThreads = 1,
			bool faultOutOfOrderProjections = false,
			int maxAutoMergeIndexLevel = 1000,
			bool disableFirstLevelHttpAuthorization = false,
			bool logFailedAuthenticationAttempts = false,
			long maxTruncation = 256 * 1024 * 1024,
			bool readOnlyReplica = false,
			int maxAppendSize = 1024 * 1024,
			bool unsafeAllowSurplusNodes = false,
			bool enableExternalTCP = false,
			bool enableAtomPubOverHTTP = true,
			bool disableHttps = false) {
			Ensure.NotEmptyGuid(instanceId, "instanceId");
			Ensure.Equal(false, internalTcpEndPoint == null && internalSecureTcpEndPoint == null, "Both internal TCP endpoints are null");

			Ensure.NotNull(httpEndPoint, nameof(httpEndPoint));
			if ((clusterNodeCount>1 && internalSecureTcpEndPoint != null) || externalSecureTcpEndPoint != null || !disableHttps)
				Ensure.NotNull(certificate, "certificate");
			Ensure.Positive(workerThreads, "workerThreads");
			Ensure.NotNull(clusterDns, "clusterDns");
			Ensure.NotNull(gossipSeeds, "gossipSeeds");
			Ensure.Positive(clusterNodeCount, "clusterNodeCount");
			Ensure.Positive(prepareAckCount, "prepareAckCount");
			Ensure.Positive(commitAckCount, "commitAckCount");
			Ensure.Positive(initializationThreads, "initializationThreads");
			Ensure.NotNull(gossipAdvertiseInfo, "gossipAdvertiseInfo");
			if (maxAppendSize > TFConsts.EffectiveMaxLogRecordSize) {
				throw new ArgumentOutOfRangeException(nameof(maxAppendSize), $"{nameof(maxAppendSize)} exceeded {TFConsts.EffectiveMaxLogRecordSize} bytes.");
			}

			if (discoverViaDns && string.IsNullOrWhiteSpace(clusterDns))
				throw new ArgumentException(
					"Either DNS Discovery must be disabled (and seeds specified), or a cluster DNS name must be provided.");

			LoadConfigFunc = loadConfigFunc;
			NodeInfo = new VNodeInfo(instanceId, debugIndex,
				internalTcpEndPoint, internalSecureTcpEndPoint,
				externalTcpEndPoint, externalSecureTcpEndPoint,
				httpEndPoint,
				readOnlyReplica);
			GossipAdvertiseInfo = gossipAdvertiseInfo;
			EnableTrustedAuth = enableTrustedAuth;
			Certificate = certificate;
			TrustedRootCerts = trustedRootCerts;
			CertificateReservedNodeCommonName = certificateReservedNodeCommonName;

			WorkerThreads = workerThreads;
			StartStandardProjections = startStandardProjections;
			EnableAtomPubOverHTTP = enableAtomPubOverHTTP;
			DisableHTTPCaching = disableHTTPCaching;
			LogHttpRequests = logHttpRequests;
			LogFailedAuthenticationAttempts = logFailedAuthenticationAttempts;
			AdditionalConsumerStrategies =
				additionalConsumerStrategies ?? new IPersistentSubscriptionConsumerStrategyFactory[0];

			DiscoverViaDns = discoverViaDns;
			ClusterDns = clusterDns;
			GossipSeeds = gossipSeeds;
			GossipOnSingleNode = gossipOnSingleNode;

			ClusterNodeCount = clusterNodeCount;
			MinFlushDelay = minFlushDelay;
			PrepareAckCount = prepareAckCount;
			CommitAckCount = commitAckCount;
			PrepareTimeout = prepareTimeout;
			CommitTimeout = commitTimeout;
			WriteTimeout = writeTimeout;

			DisableInternalTcpTls = disableInternalTcpTls;
			DisableExternalTcpTls = disableExternalTcpTls;
			DisableHttps = disableHttps;
			EnableExternalTCP = enableExternalTCP;

			StatsPeriod = statsPeriod;
			StatsStorage = statsStorage;

			AuthenticationProviderFactory = authenticationProviderFactory;
			AuthorizationProviderFactory = authorizationProviderFactory;
			DisableFirstLevelHttpAuthorization = disableFirstLevelHttpAuthorization;

			NodePriority = nodePriority;
			DisableScavengeMerging = disableScavengeMerging;
			ScavengeHistoryMaxAge = scavengeHistoryMaxAge;
			AdminOnPublic = adminOnPublic;
			StatsOnPublic = statsOnPublic;
			GossipOnPublic = gossipOnPublic;
			GossipInterval = gossipInterval;
			GossipAllowedTimeDifference = gossipAllowedTimeDifference;
			GossipTimeout = gossipTimeout;
			IntTcpHeartbeatTimeout = intTcpHeartbeatTimeout;
			IntTcpHeartbeatInterval = intTcpHeartbeatInterval;
			ExtTcpHeartbeatTimeout = extTcpHeartbeatTimeout;
			ExtTcpHeartbeatInterval = extTcpHeartbeatInterval;
			ConnectionPendingSendBytesThreshold = connectionPendingSendBytesThreshold;
			ConnectionQueueSizeThreshold = connectionQueueSizeThreshold;
			DeadMemberRemovalPeriod = deadMemberRemovalPeriod;

			VerifyDbHash = verifyDbHash;
			MaxMemtableEntryCount = maxMemtableEntryCount;
			HashCollisionReadLimit = hashCollisionReadLimit;

			EnableHistograms = enableHistograms;
			SkipIndexVerify = skipIndexVerify;
			IndexCacheDepth = indexCacheDepth;
			IndexBitnessVersion = indexBitnessVersion;
			OptimizeIndexMerge = optimizeIndexMerge;
			Index = index;
			UnsafeIgnoreHardDeletes = unsafeIgnoreHardDeletes;
			ReaderThreadsCount = readerThreadsCount;
			AlwaysKeepScavenged = alwaysKeepScavenged;
			SkipIndexScanOnReads = skipIndexScanOnReads;
			ReduceFileCachePressure = reduceFileCachePressure;
			InitializationThreads = initializationThreads;
			MaxAutoMergeIndexLevel = maxAutoMergeIndexLevel;
			FaultOutOfOrderProjections = faultOutOfOrderProjections;
			ReadOnlyReplica = readOnlyReplica;
			MaxAppendSize = maxAppendSize;
			PTableMaxReaderCount = ptableMaxReaderCount;
			UnsafeAllowSurplusNodes = unsafeAllowSurplusNodes;
			MaxTruncation = maxTruncation;
		}

		public override string ToString() =>
			$"InstanceId: {NodeInfo.InstanceId}\n" + $"InternalTcp: {NodeInfo.InternalTcp}\n" +
			$"InternalSecureTcp: {NodeInfo.InternalSecureTcp}\n" + $"ExternalTcp: {NodeInfo.ExternalTcp}\n" +
			$"ExternalSecureTcp: {NodeInfo.ExternalSecureTcp}\n" +
			$"HttpEndPoint: {NodeInfo.HttpEndPoint}\n" +
			$"EnableTrustedAuth: {EnableTrustedAuth}\n" +
			$"Certificate: {(Certificate == null ? "n/a" : Certificate.ToString(true))}\n" +
			$"Trusted Root Certificates: {(TrustedRootCerts == null ? "n/a" : TrustedRootCerts.ToString())}\n" +
			$"LogHttpRequests: {LogHttpRequests}\n" + $"WorkerThreads: {WorkerThreads}\n" +
			$"DiscoverViaDns: {DiscoverViaDns}\n" + $"ClusterDns: {ClusterDns}\n" +
			$"GossipSeeds: {string.Join(",", GossipSeeds.Select(x => x.ToString()))}\n" +
			$"ClusterNodeCount: {ClusterNodeCount}\n" + $"MinFlushDelay: {MinFlushDelay}\n" +
			$"PrepareAckCount: {PrepareAckCount}\n" + $"CommitAckCount: {CommitAckCount}\n" +
			$"PrepareTimeout: {PrepareTimeout}\n" + $"CommitTimeout: {CommitTimeout}\n" +
			$"WriteTimeout: {WriteTimeout}\n" +
			$"DisableInternalTcpTls: {DisableInternalTcpTls}\n" + $"DisableExternalTcpTls: {DisableExternalTcpTls}\n" +
			$"DisableHttps: {DisableHttps}\n" +
			$"StatsPeriod: {StatsPeriod}\n" + $"StatsStorage: {StatsStorage}\n" +
			$"AuthenticationProviderFactory Type: {AuthenticationProviderFactory.GetType()}\n" +
			$"AuthorizationProviderFactory  Type: {AuthorizationProviderFactory.GetType()}\n" +
			$"NodePriority: {NodePriority}" + $"GossipInterval: {GossipInterval}\n" +
			$"GossipAllowedTimeDifference: {GossipAllowedTimeDifference}\n" +
			$"GossipTimeout: {GossipTimeout}\n" + $"HistogramEnabled: {EnableHistograms}\n" +
			$"HTTPCachingDisabled: {DisableHTTPCaching}\n" + $"IndexPath: {Index}\n" +
			$"ScavengeHistoryMaxAge: {ScavengeHistoryMaxAge}\n" +
			$"ConnectionPendingSendBytesThreshold: {ConnectionPendingSendBytesThreshold}\n" +
			$"ReduceFileCachePressure: {ReduceFileCachePressure}\n" +
			$"InitializationThreads: {InitializationThreads}\n" +
			$"DisableFirstLevelHttpAuthorization: {DisableFirstLevelHttpAuthorization}\n" +
			$"ReadOnlyReplica: {ReadOnlyReplica}\n" +
			$"UnsafeAllowSurplusNodes: {UnsafeAllowSurplusNodes}\n" +
			$"DeadMemberRemovalPeriod: {DeadMemberRemovalPeriod}\n" +
			$"MaxTruncation: {MaxTruncation}\n";
	}
}
