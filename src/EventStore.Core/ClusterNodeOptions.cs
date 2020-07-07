using System.Net;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Util;
using EventStore.Rags;

namespace EventStore.Core {
	public class ClusterNodeOptions : IOptions {
		[ArgDescription(Opts.ShowHelpDescr, Opts.AppGroup)]
		public bool Help { get; set; }

		[ArgDescription(Opts.ShowVersionDescr, Opts.AppGroup)]
		public bool Version { get; set; }

		[ArgDescription(Opts.LogsDescr, Opts.AppGroup)]
		public string Log { get; set; }

		[ArgDescription(Opts.ConfigsDescr, Opts.AppGroup)]
		public string Config { get; set; }

		[ArgDescription(Opts.WhatIfDescr, Opts.AppGroup)]
		public bool WhatIf { get; set; }

		[ArgDescription(Opts.StartStandardProjectionsDescr, Opts.AppGroup)]
		public bool StartStandardProjections { get; set; }

		[ArgDescription(Opts.DisableHttpCachingDescr, Opts.AppGroup)]
		public bool DisableHTTPCaching { get; set; }

		[ArgDescription(Opts.InternalIpDescr, Opts.InterfacesGroup)]
		public IPAddress IntIp { get; set; }

		[ArgDescription(Opts.ExternalIpDescr, Opts.InterfacesGroup)]
		public IPAddress ExtIp { get; set; }

		[ArgDescription(Opts.HttpPortDescr, Opts.InterfacesGroup)]
		public int HttpPort { get; set; }

		[ArgDescription(Opts.EnableExternalTCPDescr, Opts.InterfacesGroup)]
		public bool EnableExternalTCP { get; set; }
		
		[ArgDescription(Opts.InternalTcpPortDescr, Opts.InterfacesGroup)]
		public int IntTcpPort { get; set; }

		[ArgDescription(Opts.ExternalTcpPortDescr, Opts.InterfacesGroup)]
		public int ExtTcpPort { get; set; }

		[ArgDescription(Opts.ExternalIpAdvertiseAsDescr, Opts.InterfacesGroup)]
		public string ExtHostAdvertiseAs { get; set; }

		[ArgDescription(Opts.ExternalTcpPortAdvertiseAsDescr, Opts.InterfacesGroup)]
		public int ExtTcpPortAdvertiseAs { get; set; }

		[ArgDescription(Opts.HttpPortAdvertiseAsDescr, Opts.InterfacesGroup)]
		public int HttpPortAdvertiseAs { get; set; }

		[ArgDescription(Opts.InternalIpAdvertiseAsDescr, Opts.InterfacesGroup)]
		public string IntHostAdvertiseAs { get; set; }

		[ArgDescription(Opts.InternalTcpPortAdvertiseAsDescr, Opts.InterfacesGroup)]
		public int IntTcpPortAdvertiseAs { get; set; }

		[ArgDescription(Opts.IntTcpHeartbeatTimeoutDescr, Opts.InterfacesGroup)]
		public int IntTcpHeartbeatTimeout { get; set; }

		[ArgDescription(Opts.ExtTcpHeartbeatTimeoutDescr, Opts.InterfacesGroup)]
		public int ExtTcpHeartbeatTimeout { get; set; }

		[ArgDescription(Opts.IntTcpHeartbeatIntervalDescr, Opts.InterfacesGroup)]
		public int IntTcpHeartbeatInterval { get; set; }

		[ArgDescription(Opts.ExtTcpHeartbeatIntervalDescr, Opts.InterfacesGroup)]
		public int ExtTcpHeartbeatInterval { get; set; }

		[ArgDescription(Opts.GossipOnSingleNodeDescr, Opts.InterfacesGroup)]
		public bool GossipOnSingleNode { get; set; }

		[ArgDescription(Opts.ConnectionPendingSendBytesThresholdDescr, Opts.InterfacesGroup)]
		public int ConnectionPendingSendBytesThreshold { get; set; }
		
		[ArgDescription(Opts.ConnectionQueueSizeThresholdDescr, Opts.InterfacesGroup)]
		public int ConnectionQueueSizeThreshold { get; set; }

		[ArgDescription(Opts.ClusterSizeDescr, Opts.ClusterGroup)]
		public int ClusterSize { get; set; }

		[ArgDescription(Opts.NodePriorityDescr, Opts.ClusterGroup)]
		public int NodePriority { get; set; }

		[ArgDescription(Opts.MinFlushDelayMsDescr, Opts.DbGroup)]
		public double MinFlushDelayMs { get; set; }

		[ArgDescription(Opts.CommitCountDescr, Opts.ClusterGroup)]
		public int CommitCount { get; set; }

		[ArgDescription(Opts.PrepareCountDescr, Opts.ClusterGroup)]
		public int PrepareCount { get; set; }

		[ArgDescription(Opts.DisableAdminUiDescr, Opts.InterfacesGroup)]
		public bool DisableAdminUi { get; set; }

		[ArgDescription(Opts.DisableStatsOnHttpDescr, Opts.InterfacesGroup)]
		public bool DisableStatsOnHttp { get; set; }
	
		[ArgDescription(Opts.DisableGossipOnHttpDescr, Opts.InterfacesGroup)]
		public bool DisableGossipOnHttp { get; set; }

		[ArgDescription(Opts.DisableScavengeMergeDescr, Opts.DbGroup)]
		public bool DisableScavengeMerging { get; set; }

		[ArgDescription(Opts.ScavengeHistoryMaxAgeDescr, Opts.DbGroup)]
		public int ScavengeHistoryMaxAge { get; set; }

		[ArgDescription(Opts.DiscoverViaDnsDescr, Opts.ClusterGroup)]
		public bool DiscoverViaDns { get; set; }

		[ArgDescription(Opts.ClusterDnsDescr, Opts.ClusterGroup)]
		public string ClusterDns { get; set; }

		[ArgDescription(Opts.ClusterGossipPortDescr, Opts.ClusterGroup)]
		public int ClusterGossipPort { get; set; }

		[ArgDescription(Opts.GossipSeedDescr, Opts.ClusterGroup)]
		public EndPoint[] GossipSeed { get; set; }

		[ArgDescription(Opts.StatsPeriodDescr, Opts.AppGroup)]
		public int StatsPeriodSec { get; set; }

		[ArgDescription(Opts.CachedChunksDescr, Opts.DbGroup)]
		public int CachedChunks { get; set; }

		[ArgDescription(Opts.ReaderThreadsCountDescr, Opts.DbGroup)]
		public int ReaderThreadsCount { get; set; }

		[ArgDescription(Opts.ChunksCacheSizeDescr, Opts.DbGroup)]
		public long ChunksCacheSize { get; set; }

		[ArgDescription(Opts.MaxMemTableSizeDescr, Opts.DbGroup)]
		public int MaxMemTableSize { get; set; }

		[ArgDescription(Opts.HashCollisionReadLimitDescr, Opts.DbGroup)]
		public int HashCollisionReadLimit { get; set; }

		[ArgDescription(Opts.DbPathDescr, Opts.DbGroup)]
		public string Db { get; set; }

		[ArgDescription(Opts.IndexPathDescr, Opts.DbGroup)]
		public string Index { get; set; }

		[ArgDescription(Opts.InMemDbDescr, Opts.DbGroup)]
		public bool MemDb { get; set; }

		[ArgDescription(Opts.SkipDbVerifyDescr, Opts.DbGroup)]
		public bool SkipDbVerify { get; set; }

		[ArgDescription(Opts.WriteThroughDescr, Opts.DbGroup)]
		public bool WriteThrough { get; set; }

		[ArgDescription(Opts.UnbufferedDescr, Opts.DbGroup)]
		public bool Unbuffered { get; set; }

		[ArgDescription(Opts.ChunkInitialReaderCountDescr, Opts.DbGroup)]
		public int ChunkInitialReaderCount { get; set; }


		[ArgDescription(Opts.RunProjectionsDescr, Opts.ProjectionsGroup)]
		public ProjectionType RunProjections { get; set; }

		[ArgDescription(Opts.ProjectionThreadsDescr, Opts.ProjectionsGroup)]
		public int ProjectionThreads { get; set; }

		[ArgDescription(Opts.WorkerThreadsDescr, Opts.AppGroup)]
		public int WorkerThreads { get; set; }

		[ArgDescription(Opts.ProjectionsQueryExpiryDescr, Opts.ProjectionsGroup)]
		public int ProjectionsQueryExpiry { get; set; }

		[ArgDescription(Opts.FaultOutOfOrderProjectionsDescr, Opts.ProjectionsGroup)]
		public bool FaultOutOfOrderProjections { get; set; }

		[ArgDescription(Opts.EnableTrustedAuthDescr, Opts.InterfacesGroup)]
		public bool EnableTrustedAuth { get; set; }

		[ArgDescription(Opts.TrustedRootCertificatesPathDescr, Opts.CertificateGroup)]
		public string TrustedRootCertificatesPath { get; set; }

		[ArgDescription(Opts.CertificateFileDescr, Opts.CertificatesFromFileGroup)]
		public string CertificateFile { get; set; }

		[ArgDescription(Opts.CertificatePrivateKeyFileDescr, Opts.CertificatesFromFileGroup)]
		public string CertificatePrivateKeyFile { get; set; }

		[ArgMask, ArgDescription(Opts.CertificatePasswordDescr, Opts.CertificatesFromFileGroup)]
		public string CertificatePassword { get; set; }

		[ArgDescription(Opts.CertificateStoreLocationDescr, Opts.CertificatesFromStoreGroup)]
		public string CertificateStoreLocation { get; set; }

		[ArgDescription(Opts.CertificateStoreNameDescr, Opts.CertificatesFromStoreGroup)]
		public string CertificateStoreName { get; set; }

		[ArgDescription(Opts.CertificateSubjectNameDescr, Opts.CertificatesFromStoreGroup)]
		public string CertificateSubjectName { get; set; }
		[ArgDescription(Opts.CertificateReservedNodeCommonNameDescr, Opts.CertificateGroup)]
		public string CertificateReservedNodeCommonName { get; set; }

		[ArgDescription(Opts.CertificateThumbprintDescr, Opts.CertificatesFromStoreGroup)]
		public string CertificateThumbprint { get; set; }

		[ArgDescription(Opts.DisableInternalTcpTlsDescr, Opts.InterfacesGroup)]
		public bool DisableInternalTcpTls { get; set; }

		[ArgDescription(Opts.DisableExternalTcpTlsDescr, Opts.InterfacesGroup)]
		public bool DisableExternalTcpTls { get; set; }

		[ArgDescription(Opts.DisableHttpsDescr, Opts.InterfacesGroup)]
		public bool DisableHttps { get; set; }

		[ArgDescription(Opts.AuthorizationTypeDescr, Opts.AuthGroup)]
		public string AuthorizationType { get; set; }

		[ArgDescription(Opts.AuthenticationTypeDescr, Opts.AuthGroup)]
		public string AuthenticationType { get; set; }

		[ArgDescription(Opts.AuthorizationConfigFileDescr, Opts.AuthGroup)]
		public string AuthorizationConfig { get; set; }
		
		[ArgDescription(Opts.AuthenticationConfigFileDescr, Opts.AuthGroup)]
		public string AuthenticationConfig { get; set; }

		[ArgDescription(Opts.DisableFirstLevelHttpAuthorizationDescr, Opts.AuthGroup)]
		public bool DisableFirstLevelHttpAuthorization { get; set; }

		[ArgDescription(Opts.PrepareTimeoutMsDescr, Opts.DbGroup)]
		public int PrepareTimeoutMs { get; set; }

		[ArgDescription(Opts.CommitTimeoutMsDescr, Opts.DbGroup)]
		public int CommitTimeoutMs { get; set; }

		[ArgDescription(Opts.WriteTimeoutMsDescr, Opts.DbGroup)]
		public int WriteTimeoutMs { get; set; }

		[ArgDescription(Opts.UnsafeDisableFlushToDiskDescr, Opts.DbGroup)]
		public bool UnsafeDisableFlushToDisk { get; set; }

		[ArgDescription(Opts.UnsafeIgnoreHardDeleteDescr, Opts.DbGroup)]
		public bool UnsafeIgnoreHardDelete { get; set; }

		[ArgDescription(Opts.SkipIndexVerifyDescr, Opts.DbGroup)]
		public bool SkipIndexVerify { get; set; }

		[ArgDescription(Opts.IndexCacheDepthDescr, Opts.DbGroup)]
		public int IndexCacheDepth { get; set; }

		[ArgDescription(Opts.OptimizeIndexMergeDescr, Opts.DbGroup)]
		public bool OptimizeIndexMerge { get; set; }

		[ArgDescription(Opts.GossipIntervalMsDescr, Opts.ClusterGroup)]
		public int GossipIntervalMs { get; set; }

		[ArgDescription(Opts.GossipAllowedDifferenceMsDescr, Opts.ClusterGroup)]
		public int GossipAllowedDifferenceMs { get; set; }

		[ArgDescription(Opts.GossipTimeoutMsDescr, Opts.ClusterGroup)]
		public int GossipTimeoutMs { get; set; }

		[ArgDescription(Opts.ReadOnlyReplicaDescr, Opts.ClusterGroup)]
		public bool ReadOnlyReplica { get; set; }

		[ArgDescription(Opts.UnsafeAllowSurplusNodesDescr, Opts.ClusterGroup)]
		public bool UnsafeAllowSurplusNodes { get; set; }

		[ArgDescription(Opts.HistogramDescr, Opts.AppGroup)]
		public bool EnableHistograms { get; set; }

		[ArgDescription(Opts.LogHttpRequestsDescr, Opts.AppGroup)]
		public bool LogHttpRequests { get; set; }
		
		[ArgDescription(Opts.LogFailedAuthenticationAttemptsDescr, Opts.AppGroup)]
		public bool LogFailedAuthenticationAttempts { get; set; }

		[ArgDescription(Opts.AlwaysKeepScavengedDescr, Opts.DbGroup)]
		public bool AlwaysKeepScavenged { get; set; }

		[ArgDescription(Opts.SkipIndexScanOnReadsDescr, Opts.AppGroup)]
		public bool SkipIndexScanOnReads { get; set; }

		[ArgDescription(Opts.ReduceFileCachePressureDescr, Opts.DbGroup)]
		public bool ReduceFileCachePressure { get; set; }

		[ArgDescription(Opts.InitializationThreadsDescr, Opts.DbGroup)]
		public int InitializationThreads { get; set; }

		[ArgDescription(Opts.MaxAutoMergeIndexLevelDescr, Opts.DbGroup)]
		public int MaxAutoMergeIndexLevel { get; set; }

		[ArgDescription(Opts.WriteStatsToDbDescr, Opts.DbGroup)]
		public bool WriteStatsToDb { get; set; }
		
		[ArgDescription(Opts.MaxTruncationDescr, Opts.DbGroup)]
		public long MaxTruncation { get; set; }

		[ArgDescription(Opts.MaxAppendSizeDecr, Opts.AppGroup)]
		public int MaxAppendSize { get; set; }
		
		[ArgDescription(Opts.DevDescr, Opts.AppGroup)]
		public bool Dev { get; set; }

		[ArgDescription(Opts.InsecureDescr, Opts.AppGroup)]
		public bool Insecure { get; set; }
		
		[ArgDescription(Opts.EnableAtomPubOverHTTPDescr, Opts.InterfacesGroup)]
		public bool EnableAtomPubOverHTTP { get; set; }

		[ArgDescription(Opts.DeadMemberRemovalPeriodDescr, Opts.ClusterGroup)]
		public int DeadMemberRemovalPeriodSec { get; set; }
		
		public ClusterNodeOptions() {
			Config = "";
			Help = Opts.ShowHelpDefault;
			Version = Opts.ShowVersionDefault;
			Log = Locations.DefaultLogDirectory;
			WhatIf = Opts.WhatIfDefault;

			IntIp = Opts.InternalIpDefault;
			ExtIp = Opts.ExternalIpDefault;
			HttpPort = Opts.HttpPortDefault;
			EnableExternalTCP = Opts.EnableExternalTCPDefault;
			IntTcpPort = Opts.InternalTcpPortDefault;
			ExtTcpPort = Opts.ExternalTcpPortDefault;
			ClusterSize = Opts.ClusterSizeDefault;
			MinFlushDelayMs = Opts.MinFlushDelayMsDefault;
			NodePriority = Opts.NodePriorityDefault;
			GossipOnSingleNode = Opts.GossipOnSingleNodeDefault;

			CommitCount = Opts.CommitCountDefault;
			PrepareCount = Opts.PrepareCountDefault;
			MaxMemTableSize = Opts.MaxMemtableSizeDefault;
			HashCollisionReadLimit = Opts.HashCollisionReadLimitDefault;

			DiscoverViaDns = Opts.DiscoverViaDnsDefault;
			ClusterDns = Opts.ClusterDnsDefault;
			ClusterGossipPort = Opts.ClusterGossipPortDefault;
			GossipSeed = Opts.GossipSeedDefault;
			ReadOnlyReplica = Opts.ReadOnlyReplicaDefault;
			UnsafeAllowSurplusNodes = Opts.UnsafeAllowSurplusNodesDefault;
			DeadMemberRemovalPeriodSec = Opts.DeadMemberRemovalPeriodDefault;

			StatsPeriodSec = Opts.StatsPeriodDefault;
			CachedChunks = Opts.CachedChunksDefault;
			ChunksCacheSize = Opts.ChunksCacheSizeDefault;

			Db = Locations.DefaultDataDirectory;
			MemDb = Opts.InMemDbDefault;
			SkipDbVerify = Opts.SkipDbVerifyDefault;
			RunProjections = Opts.RunProjectionsDefault;
			ProjectionThreads = Opts.ProjectionThreadsDefault;
			ProjectionsQueryExpiry = Opts.ProjectionsQueryExpiryDefault;
			FaultOutOfOrderProjections = Opts.FaultOutOfOrderProjectionsDefault;
			WorkerThreads = Opts.WorkerThreadsDefault;

			EnableTrustedAuth = Opts.EnableTrustedAuthDefault;

			EnableAtomPubOverHTTP = Opts.EnableAtomPubOverHTTPDefault;

			ExtTcpHeartbeatTimeout = Opts.ExtTcpHeartbeatTimeoutDefault;
			IntTcpHeartbeatTimeout = Opts.IntTcpHeartbeatTimeoutDefault;

			ExtTcpHeartbeatInterval = Opts.ExtTcpHeartbeatIntervalDefault;
			IntTcpHeartbeatInterval = Opts.IntTcpHeartbeatIntervalDefault;

			ExtHostAdvertiseAs = Opts.ExternalHostAdvertiseAsDefault;
			ExtTcpPortAdvertiseAs = Opts.ExternalTcpPortAdvertiseAsDefault;
			HttpPortAdvertiseAs = Opts.HttpPortAdvertiseAsDefault;

			IntHostAdvertiseAs = Opts.InternalHostAdvertiseAsDefault;
			IntTcpPortAdvertiseAs = Opts.InternalTcpPortAdvertiseAsDefault;

			CertificateStoreLocation = Opts.CertificateStoreLocationDefault;
			CertificateStoreName = Opts.CertificateStoreNameDefault;
			CertificateSubjectName = Opts.CertificateSubjectNameDefault;
			CertificateReservedNodeCommonName = Opts.CertificateReservedNodeCommonNameDefault;
			CertificateThumbprint = Opts.CertificateThumbprintDefault;

			TrustedRootCertificatesPath = Opts.TrustedRootCertificatesPathDefault;
			CertificateFile = Opts.CertificateFileDefault;
			CertificatePrivateKeyFile = Opts.CertificatePrivateKeyFileDefault;
			CertificatePassword = Opts.CertificatePasswordDefault;

			DisableInternalTcpTls = Opts.DisableInternalTcpTlsDefault;
			DisableExternalTcpTls = Opts.DisableExternalTcpTlsDefault;
			DisableHttps = Opts.DisableHttpsDefault;

			AuthorizationType = Opts.AuthorizationTypeDefault;
			AuthorizationConfig = Opts.AuthorizationConfigFileDefault;
			AuthenticationType = Opts.AuthenticationTypeDefault;
			AuthenticationConfig = Opts.AuthenticationConfigFileDefault;
			DisableFirstLevelHttpAuthorization = Opts.DisableFirstLevelHttpAuthorizationDefault;

			UnsafeIgnoreHardDelete = Opts.UnsafeIgnoreHardDeleteDefault;
			UnsafeDisableFlushToDisk = Opts.UnsafeDisableFlushToDiskDefault;
			PrepareTimeoutMs = Opts.PrepareTimeoutMsDefault;
			CommitTimeoutMs = Opts.CommitTimeoutMsDefault;
			WriteTimeoutMs = Opts.WriteTimeoutMsDefault;
			DisableScavengeMerging = Opts.DisableScavengeMergeDefault;
			ScavengeHistoryMaxAge = Opts.ScavengeHistoryMaxAgeDefault;
			DisableStatsOnHttp = Opts.DisableStatsOnHttpDefault;
			DisableAdminUi = Opts.DisableAdminUiDefault;
			GossipIntervalMs = Opts.GossipIntervalMsDefault;
			GossipAllowedDifferenceMs = Opts.GossipAllowedDifferenceMsDefault;
			GossipTimeoutMs = Opts.GossipTimeoutMsDefault;
			IndexCacheDepth = Opts.IndexCacheDepthDefault;
			SkipIndexVerify = Opts.SkipIndexVerifyDefault;
			OptimizeIndexMerge = Opts.OptimizeIndexMergeDefault;
			DisableGossipOnHttp = Opts.DisableGossipOnHttpDefault;
			EnableHistograms = Opts.HistogramEnabledDefault;
			ReaderThreadsCount = Opts.ReaderThreadsCountDefault;

			StartStandardProjections = Opts.StartStandardProjectionsDefault;
			DisableHTTPCaching = Opts.DisableHttpCachingDefault;
			LogHttpRequests = Opts.LogHttpRequestsDefault;
			LogFailedAuthenticationAttempts = Opts.LogFailedAuthenticationAttemptsDefault;

			Unbuffered = Opts.UnbufferedDefault;
			WriteThrough = Opts.WriteThroughDefault;

			AlwaysKeepScavenged = Opts.AlwaysKeepScavengedDefault;

			SkipIndexScanOnReads = Opts.SkipIndexScanOnReadsDefault;
			ReduceFileCachePressure = Opts.ReduceFileCachePressureDefault;
			InitializationThreads = Opts.InitializationThreadsDefault;

			ConnectionPendingSendBytesThreshold = Opts.ConnectionPendingSendBytesThresholdDefault;
			ConnectionQueueSizeThreshold = Opts.ConnectionQueueSizeThresholdDefault;
			ChunkInitialReaderCount = Opts.ChunkInitialReaderCountDefault;

			MaxAutoMergeIndexLevel = Opts.MaxAutoMergeIndexLevelDefault;

			WriteStatsToDb = Opts.WriteStatsToDbDefault;
			MaxTruncation = Opts.MaxTruncationDefault;
			MaxAppendSize = Opts.MaxAppendSizeDefault;

			Dev = Opts.DevDefault;
			Insecure = Opts.InsecureDefault;
		}
	}
}
