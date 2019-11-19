using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Utils;
using EventStore.Core.Authentication;
using EventStore.Core.Data;
using EventStore.Core.Services.Monitoring;
using EventStore.Core.Services.PersistentSubscription.ConsumerStrategy;

namespace EventStore.Core.Cluster.Settings {
	public class ClusterVNodeSettings {
		public readonly VNodeInfo NodeInfo;
		public readonly GossipAdvertiseInfo GossipAdvertiseInfo;
		public readonly string[] IntHttpPrefixes;
		public readonly string[] ExtHttpPrefixes;
		public readonly bool EnableTrustedAuth;
		public readonly X509Certificate2 Certificate;
		public readonly int WorkerThreads;
		public readonly bool StartStandardProjections;
		public readonly bool DisableHTTPCaching;
		public readonly bool LogHttpRequests;
		public readonly bool LogFailedAuthenticationAttempts;

		public readonly bool DiscoverViaDns;
		public readonly string ClusterDns;
		public readonly IPEndPoint[] GossipSeeds;
		public readonly bool EnableHistograms;
		public readonly TimeSpan MinFlushDelay;

		public readonly int ClusterNodeCount;
		public readonly int PrepareAckCount;
		public readonly int CommitAckCount;
		public readonly TimeSpan PrepareTimeout;
		public readonly TimeSpan CommitTimeout;

		public readonly int NodePriority;

		public readonly bool UseSsl;
		public readonly bool DisableInsecureTCP;
		public readonly string SslTargetHost;
		public readonly bool SslValidateServer;

		public readonly TimeSpan StatsPeriod;
		public readonly StatsStorage StatsStorage;

		public readonly IAuthenticationProviderFactory AuthenticationProviderFactory;
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

		public readonly bool BetterOrdering;
		public readonly string Index;
		public readonly int ReaderThreadsCount;
		public readonly IPersistentSubscriptionConsumerStrategyFactory[] AdditionalConsumerStrategies;
		public readonly bool AlwaysKeepScavenged;
		public readonly bool SkipIndexScanOnReads;
		public readonly bool ReduceFileCachePressure;
		public readonly int InitializationThreads;
		public readonly int MaxAutoMergeIndexLevel;

		public readonly bool GossipOnSingleNode;
		public readonly bool FaultOutOfOrderProjections;
		public readonly bool StructuredLog;
		public int PTableMaxReaderCount;

		public ClusterVNodeSettings(Guid instanceId, int debugIndex,
			IPEndPoint internalTcpEndPoint,
			IPEndPoint internalSecureTcpEndPoint,
			IPEndPoint externalTcpEndPoint,
			IPEndPoint externalSecureTcpEndPoint,
			IPEndPoint internalHttpEndPoint,
			IPEndPoint externalHttpEndPoint,
			GossipAdvertiseInfo gossipAdvertiseInfo,
			string[] intHttpPrefixes,
			string[] extHttpPrefixes,
			bool enableTrustedAuth,
			X509Certificate2 certificate,
			int workerThreads,
			bool discoverViaDns,
			string clusterDns,
			IPEndPoint[] gossipSeeds,
			TimeSpan minFlushDelay,
			int clusterNodeCount,
			int prepareAckCount,
			int commitAckCount,
			TimeSpan prepareTimeout,
			TimeSpan commitTimeout,
			bool useSsl,
			bool disableInsecureTCP,
			string sslTargetHost,
			bool sslValidateServer,
			TimeSpan statsPeriod,
			StatsStorage statsStorage,
			int nodePriority,
			IAuthenticationProviderFactory authenticationProviderFactory,
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
			bool betterOrdering = false,
			int readerThreadsCount = 4,
			bool alwaysKeepScavenged = false,
			bool gossipOnSingleNode = false,
			bool skipIndexScanOnReads = false,
			bool reduceFileCachePressure = false,
			int initializationThreads = 1,
			bool faultOutOfOrderProjections = false,
			bool structuredLog = false,
			int maxAutoMergeIndexLevel = 1000,
			bool disableFirstLevelHttpAuthorization = false,
			bool logFailedAuthenticationAttempts = false) {
			Ensure.NotEmptyGuid(instanceId, "instanceId");
			Ensure.NotNull(internalTcpEndPoint, "internalTcpEndPoint");
			Ensure.NotNull(externalTcpEndPoint, "externalTcpEndPoint");
			Ensure.NotNull(internalHttpEndPoint, "internalHttpEndPoint");
			Ensure.NotNull(externalHttpEndPoint, "externalHttpEndPoint");
			Ensure.NotNull(intHttpPrefixes, "intHttpPrefixes");
			Ensure.NotNull(extHttpPrefixes, "extHttpPrefixes");
			if (internalSecureTcpEndPoint != null || externalSecureTcpEndPoint != null)
				Ensure.NotNull(certificate, "certificate");
			Ensure.Positive(workerThreads, "workerThreads");
			Ensure.NotNull(clusterDns, "clusterDns");
			Ensure.NotNull(gossipSeeds, "gossipSeeds");
			Ensure.Positive(clusterNodeCount, "clusterNodeCount");
			Ensure.Positive(prepareAckCount, "prepareAckCount");
			Ensure.Positive(commitAckCount, "commitAckCount");
			Ensure.Positive(initializationThreads, "initializationThreads");
			Ensure.NotNull(gossipAdvertiseInfo, "gossipAdvertiseInfo");

			if (discoverViaDns && string.IsNullOrWhiteSpace(clusterDns))
				throw new ArgumentException(
					"Either DNS Discovery must be disabled (and seeds specified), or a cluster DNS name must be provided.");

			if (useSsl)
				Ensure.NotNull(sslTargetHost, "sslTargetHost");

			NodeInfo = new VNodeInfo(instanceId, debugIndex,
				internalTcpEndPoint, internalSecureTcpEndPoint,
				externalTcpEndPoint, externalSecureTcpEndPoint,
				internalHttpEndPoint, externalHttpEndPoint);
			GossipAdvertiseInfo = gossipAdvertiseInfo;
			IntHttpPrefixes = intHttpPrefixes;
			ExtHttpPrefixes = extHttpPrefixes;
			EnableTrustedAuth = enableTrustedAuth;
			Certificate = certificate;
			WorkerThreads = workerThreads;
			StartStandardProjections = startStandardProjections;
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

			UseSsl = useSsl;
			DisableInsecureTCP = disableInsecureTCP;
			SslTargetHost = sslTargetHost;
			SslValidateServer = sslValidateServer;

			StatsPeriod = statsPeriod;
			StatsStorage = statsStorage;

			AuthenticationProviderFactory = authenticationProviderFactory;
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
			BetterOrdering = betterOrdering;
			ReaderThreadsCount = readerThreadsCount;
			AlwaysKeepScavenged = alwaysKeepScavenged;
			SkipIndexScanOnReads = skipIndexScanOnReads;
			ReduceFileCachePressure = reduceFileCachePressure;
			InitializationThreads = initializationThreads;
			MaxAutoMergeIndexLevel = maxAutoMergeIndexLevel;
			FaultOutOfOrderProjections = faultOutOfOrderProjections;
			StructuredLog = structuredLog;
			PTableMaxReaderCount = ptableMaxReaderCount;
		}

		public override string ToString() {
			return string.Format("InstanceId: {0}\n"
			                     + "InternalTcp: {1}\n"
			                     + "InternalSecureTcp: {2}\n"
			                     + "ExternalTcp: {3}\n"
			                     + "ExternalSecureTcp: {4}\n"
			                     + "InternalHttp: {5}\n"
			                     + "ExternalHttp: {6}\n"
			                     + "IntHttpPrefixes: {7}\n"
			                     + "ExtHttpPrefixes: {8}\n"
			                     + "EnableTrustedAuth: {9}\n"
			                     + "Certificate: {10}\n"
			                     + "LogHttpRequests: {11}\n"
			                     + "WorkerThreads: {12}\n"
			                     + "DiscoverViaDns: {13}\n"
			                     + "ClusterDns: {14}\n"
			                     + "GossipSeeds: {15}\n"
			                     + "ClusterNodeCount: {16}\n"
			                     + "MinFlushDelay: {17}\n"
			                     + "PrepareAckCount: {18}\n"
			                     + "CommitAckCount: {19}\n"
			                     + "PrepareTimeout: {20}\n"
			                     + "CommitTimeout: {21}\n"
			                     + "UseSsl: {22}\n"
			                     + "SslTargetHost: {23}\n"
			                     + "SslValidateServer: {24}\n"
			                     + "StatsPeriod: {25}\n"
			                     + "StatsStorage: {26}\n"
			                     + "AuthenticationProviderFactory Type: {27}\n"
			                     + "NodePriority: {28}"
			                     + "GossipInterval: {29}\n"
			                     + "GossipAllowedTimeDifference: {30}\n"
			                     + "GossipTimeout: {31}\n"
			                     + "HistogramEnabled: {32}\n"
			                     + "HTTPCachingDisabled: {33}\n"
			                     + "IndexPath: {34}\n"
			                     + "ScavengeHistoryMaxAge: {35}\n"
			                     + "ConnectionPendingSendBytesThreshold: {36}\n"
			                     + "ReduceFileCachePressure: {37}\n"
			                     + "InitializationThreads: {38}\n"
			                     + "StructuredLog: {39}\n"
								 + "DisableFirstLevelHttpAuthorization: {40}\n",
				NodeInfo.InstanceId,
				NodeInfo.InternalTcp, NodeInfo.InternalSecureTcp,
				NodeInfo.ExternalTcp, NodeInfo.ExternalSecureTcp,
				NodeInfo.InternalHttp, NodeInfo.ExternalHttp,
				string.Join(", ", IntHttpPrefixes),
				string.Join(", ", ExtHttpPrefixes),
				EnableTrustedAuth,
				Certificate == null ? "n/a" : Certificate.ToString(true),
				LogHttpRequests,
				WorkerThreads, DiscoverViaDns, ClusterDns,
				string.Join(",", GossipSeeds.Select(x => x.ToString())),
				ClusterNodeCount, MinFlushDelay,
				PrepareAckCount, CommitAckCount, PrepareTimeout, CommitTimeout,
				UseSsl, SslTargetHost, SslValidateServer,
				StatsPeriod, StatsStorage, AuthenticationProviderFactory.GetType(),
				NodePriority, GossipInterval, GossipAllowedTimeDifference, GossipTimeout,
				EnableHistograms, DisableHTTPCaching, Index, ScavengeHistoryMaxAge,
				ConnectionPendingSendBytesThreshold,
				ReduceFileCachePressure, InitializationThreads, StructuredLog,
				DisableFirstLevelHttpAuthorization);
		}
	}
}
