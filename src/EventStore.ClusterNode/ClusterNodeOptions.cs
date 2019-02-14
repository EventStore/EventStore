using System.Net;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Util;
using EventStore.Rags;

namespace EventStore.ClusterNode {
	public class ClusterNodeOptions : IOptions {
		[ArgDescription(Opts.ShowHelpDescr, Opts.AppGroup)]
		public bool Help { get; set; }

		[ArgDescription(Opts.ShowVersionDescr, Opts.AppGroup)]
		public bool Version { get; set; }

		[ArgDescription(Opts.LogsDescr, Opts.AppGroup)]
		public string Log { get; set; }

		[ArgDescription(Opts.ConfigsDescr, Opts.AppGroup)]
		public string Config { get; set; }

		[ArgDescription(Opts.DefinesDescr, Opts.AppGroup)]
		public string[] Defines { get; set; }

		[ArgDescription(Opts.WhatIfDescr, Opts.AppGroup)]
		public bool WhatIf { get; set; }

		[ArgDescription(Opts.StartStandardProjectionsDescr, Opts.AppGroup)]
		public bool StartStandardProjections { get; set; }

		[ArgDescription(Opts.DisableHttpCachingDescr, Opts.AppGroup)]
		public bool DisableHTTPCaching { get; set; }

		[ArgDescription(Opts.MonoMinThreadpoolSizeDescr, Opts.AppGroup)]
		public int MonoMinThreadpoolSize { get; set; }

		[ArgDescription(Opts.InternalIpDescr, Opts.InterfacesGroup)]
		public IPAddress IntIp { get; set; }

		[ArgDescription(Opts.ExternalIpDescr, Opts.InterfacesGroup)]
		public IPAddress ExtIp { get; set; }

		[ArgDescription(Opts.InternalHttpPortDescr, Opts.InterfacesGroup)]
		public int IntHttpPort { get; set; }

		[ArgDescription(Opts.ExternalHttpPortDescr, Opts.InterfacesGroup)]
		public int ExtHttpPort { get; set; }

		[ArgDescription(Opts.InternalTcpPortDescr, Opts.InterfacesGroup)]
		public int IntTcpPort { get; set; }

		[ArgDescription(Opts.InternalSecureTcpPortDescr, Opts.InterfacesGroup)]
		public int IntSecureTcpPort { get; set; }

		[ArgDescription(Opts.ExternalTcpPortDescr, Opts.InterfacesGroup)]
		public int ExtTcpPort { get; set; }

		[ArgDescription(Opts.ExternalSecureTcpPortAdvertiseAsDescr, Opts.InterfacesGroup)]
		public int ExtSecureTcpPortAdvertiseAs { get; set; }

		[ArgDescription(Opts.ExternalSecureTcpPortDescr, Opts.InterfacesGroup)]
		public int ExtSecureTcpPort { get; set; }

		[ArgDescription(Opts.ExternalIpAdvertiseAsDescr, Opts.InterfacesGroup)]
		public IPAddress ExtIpAdvertiseAs { get; set; }

		[ArgDescription(Opts.ExternalTcpPortAdvertiseAsDescr, Opts.InterfacesGroup)]
		public int ExtTcpPortAdvertiseAs { get; set; }

		[ArgDescription(Opts.ExternalHttpPortAdvertiseAsDescr, Opts.InterfacesGroup)]
		public int ExtHttpPortAdvertiseAs { get; set; }

		[ArgDescription(Opts.InternalIpAdvertiseAsDescr, Opts.InterfacesGroup)]
		public IPAddress IntIpAdvertiseAs { get; set; }

		[ArgDescription(Opts.InternalSecureTcpPortAdvertiseAsDescr, Opts.InterfacesGroup)]
		public int IntSecureTcpPortAdvertiseAs { get; set; }

		[ArgDescription(Opts.InternalTcpPortAdvertiseAsDescr, Opts.InterfacesGroup)]
		public int IntTcpPortAdvertiseAs { get; set; }

		[ArgDescription(Opts.InternalHttpPortAdvertiseAsDescr, Opts.InterfacesGroup)]
		public int IntHttpPortAdvertiseAs { get; set; }

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


		[ArgDescription(Opts.ForceDescr, Opts.AppGroup)]
		public bool Force { get; set; }

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

		[ArgDescription(Opts.AdminOnExtDescr, Opts.InterfacesGroup)]
		public bool AdminOnExt { get; set; }

		[ArgDescription(Opts.StatsOnExtDescr, Opts.InterfacesGroup)]
		public bool StatsOnExt { get; set; }

		[ArgDescription(Opts.GossipOnExtDescr, Opts.InterfacesGroup)]
		public bool GossipOnExt { get; set; }

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
		public IPEndPoint[] GossipSeed { get; set; }

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

		[ArgDescription(Opts.IntHttpPrefixesDescr, Opts.InterfacesGroup)]
		public string[] IntHttpPrefixes { get; set; }

		[ArgDescription(Opts.ExtHttpPrefixesDescr, Opts.InterfacesGroup)]
		public string[] ExtHttpPrefixes { get; set; }

		[ArgDescription(Opts.EnableTrustedAuthDescr, Opts.InterfacesGroup)]
		public bool EnableTrustedAuth { get; set; }

		[ArgDescription(Opts.AddInterfacePrefixesDescr, Opts.InterfacesGroup)]
		public bool AddInterfacePrefixes { get; set; }

		[ArgDescription(Opts.CertificateStoreLocationDescr, Opts.CertificatesGroup)]
		public string CertificateStoreLocation { get; set; }

		[ArgDescription(Opts.CertificateStoreNameDescr, Opts.CertificatesGroup)]
		public string CertificateStoreName { get; set; }

		[ArgDescription(Opts.CertificateSubjectNameDescr, Opts.CertificatesGroup)]
		public string CertificateSubjectName { get; set; }

		[ArgDescription(Opts.CertificateThumbprintDescr, Opts.CertificatesGroup)]
		public string CertificateThumbprint { get; set; }

		[ArgDescription(Opts.CertificateFileDescr, Opts.CertificatesGroup)]
		public string CertificateFile { get; set; }

		[ArgDescription(Opts.CertificatePasswordDescr, Opts.CertificatesGroup)]
		public string CertificatePassword { get; set; }

		[ArgDescription(Opts.UseInternalSslDescr, Opts.InterfacesGroup)]
		public bool UseInternalSsl { get; set; }

		[ArgDescription(Opts.DisableInsecureTCPDescr, Opts.InterfacesGroup)]
		public bool DisableInsecureTCP { get; set; }

		[ArgDescription(Opts.SslTargetHostDescr, Opts.InterfacesGroup)]
		public string SslTargetHost { get; set; }

		[ArgDescription(Opts.SslValidateServerDescr, Opts.InterfacesGroup)]
		public bool SslValidateServer { get; set; }

		[ArgDescription(Opts.AuthenticationTypeDescr, Opts.AuthGroup)]
		public string AuthenticationType { get; set; }

		[ArgDescription(Opts.AuthenticationConfigFileDescr, Opts.AuthGroup)]
		public string AuthenticationConfig { get; set; }

		[ArgDescription(Opts.PrepareTimeoutMsDescr, Opts.DbGroup)]
		public int PrepareTimeoutMs { get; set; }

		[ArgDescription(Opts.CommitTimeoutMsDescr, Opts.DbGroup)]
		public int CommitTimeoutMs { get; set; }

		[ArgDescription(Opts.UnsafeDisableFlushToDiskDescr, Opts.DbGroup)]
		public bool UnsafeDisableFlushToDisk { get; set; }

		[ArgDescription(Opts.BetterOrderingDescr, Opts.DbGroup)]
		public bool BetterOrdering { get; set; }

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

		[ArgDescription(Opts.HistogramDescr, Opts.AppGroup)]
		public bool EnableHistograms { get; set; }

		[ArgDescription(Opts.LogHttpRequestsDescr, Opts.AppGroup)]
		public bool LogHttpRequests { get; set; }

		[ArgDescription(Opts.AlwaysKeepScavengedDescr, Opts.DbGroup)]
		public bool AlwaysKeepScavenged { get; set; }

		[ArgDescription(Opts.SkipIndexScanOnReadsDescr, Opts.AppGroup)]
		public bool SkipIndexScanOnReads { get; set; }

		[ArgDescription(Opts.ReduceFileCachePressureDescr, Opts.DbGroup)]
		public bool ReduceFileCachePressure { get; set; }

		[ArgDescription(Opts.InitializationThreadsDescr, Opts.DbGroup)]
		public int InitializationThreads { get; set; }

		[ArgDescription(Opts.StructuredLogDescr, Opts.DbGroup)]
		public bool StructuredLog { get; set; }

		[ArgDescription(Opts.MaxAutoMergeIndexLevelDescr, Opts.DbGroup)]
		public int MaxAutoMergeIndexLevel { get; set; }

		public ClusterNodeOptions() {
			Config = "";
			Help = Opts.ShowHelpDefault;
			Version = Opts.ShowVersionDefault;
			Log = Locations.DefaultLogDirectory;
			Defines = Opts.DefinesDefault;
			WhatIf = Opts.WhatIfDefault;

			MonoMinThreadpoolSize = Opts.MonoMinThreadpoolSizeDefault;

			IntIp = Opts.InternalIpDefault;
			ExtIp = Opts.ExternalIpDefault;
			IntHttpPort = Opts.InternalHttpPortDefault;
			ExtHttpPort = Opts.ExternalHttpPortDefault;
			IntTcpPort = Opts.InternalTcpPortDefault;
			IntSecureTcpPort = Opts.InternalSecureTcpPortDefault;
			ExtTcpPort = Opts.ExternalTcpPortDefault;
			ExtSecureTcpPort = Opts.ExternalSecureTcpPortDefault;
			Force = Opts.ForceDefault;
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

			StatsPeriodSec = Opts.StatsPeriodDefault;

			CachedChunks = Opts.CachedChunksDefault;
			ChunksCacheSize = Opts.ChunksCacheSizeDefault;

			Db = Locations.DefaultDataDirectory;
			MemDb = Opts.InMemDbDefault;
			SkipDbVerify = Opts.SkipDbVerifyDefault;
			RunProjections = Opts.RunProjectionsDefault;
			ProjectionThreads = Opts.ProjectionThreadsDefault;
			FaultOutOfOrderProjections = Opts.FaultOutOfOrderProjectionsDefault;
			WorkerThreads = Opts.WorkerThreadsDefault;
			BetterOrdering = Opts.BetterOrderingDefault;

			IntHttpPrefixes = Opts.IntHttpPrefixesDefault;
			ExtHttpPrefixes = Opts.ExtHttpPrefixesDefault;
			EnableTrustedAuth = Opts.EnableTrustedAuthDefault;
			AddInterfacePrefixes = Opts.AddInterfacePrefixesDefault;

			ExtTcpHeartbeatTimeout = Opts.ExtTcpHeartbeatTimeoutDefault;
			IntTcpHeartbeatTimeout = Opts.IntTcpHeartbeatTimeoutDefault;

			ExtTcpHeartbeatInterval = Opts.ExtTcpHeartbeatIntervalDefault;
			IntTcpHeartbeatInterval = Opts.IntTcpHeartbeatIntervalDefault;

			ExtIpAdvertiseAs = Opts.ExternalIpAdvertiseAsDefault;
			ExtTcpPortAdvertiseAs = Opts.ExternalTcpPortAdvertiseAsDefault;
			ExtHttpPortAdvertiseAs = Opts.ExternalHttpPortAdvertiseAsDefault;
			ExtSecureTcpPortAdvertiseAs = Opts.ExternalSecureTcpPortAdvertiseAsDefault;

			IntIpAdvertiseAs = Opts.InternalIpAdvertiseAsDefault;
			IntTcpPortAdvertiseAs = Opts.InternalTcpPortAdvertiseAsDefault;
			IntHttpPortAdvertiseAs = Opts.InternalHttpPortAdvertiseAsDefault;
			IntSecureTcpPortAdvertiseAs = Opts.InternalSecureTcpPortAdvertiseAsDefault;

			CertificateStoreLocation = Opts.CertificateStoreLocationDefault;
			CertificateStoreName = Opts.CertificateStoreNameDefault;
			CertificateSubjectName = Opts.CertificateSubjectNameDefault;
			CertificateThumbprint = Opts.CertificateThumbprintDefault;

			CertificateFile = Opts.CertificateFileDefault;
			CertificatePassword = Opts.CertificatePasswordDefault;

			UseInternalSsl = Opts.UseInternalSslDefault;
			DisableInsecureTCP = Opts.DisableInsecureTCPDefault;
			SslTargetHost = Opts.SslTargetHostDefault;
			SslValidateServer = Opts.SslValidateServerDefault;

			AuthenticationType = Opts.AuthenticationTypeDefault;
			AuthenticationConfig = Opts.AuthenticationConfigFileDefault;

			UnsafeIgnoreHardDelete = Opts.UnsafeIgnoreHardDeleteDefault;
			UnsafeDisableFlushToDisk = Opts.UnsafeDisableFlushToDiskDefault;
			PrepareTimeoutMs = Opts.PrepareTimeoutMsDefault;
			CommitTimeoutMs = Opts.CommitTimeoutMsDefault;
			DisableScavengeMerging = Opts.DisableScavengeMergeDefault;
			ScavengeHistoryMaxAge = Opts.ScavengeHistoryMaxAgeDefault;
			GossipOnExt = Opts.GossipOnExtDefault;
			StatsOnExt = Opts.StatsOnExtDefault;
			AdminOnExt = Opts.AdminOnExtDefault;
			GossipIntervalMs = Opts.GossipIntervalMsDefault;
			GossipAllowedDifferenceMs = Opts.GossipAllowedDifferenceMsDefault;
			GossipTimeoutMs = Opts.GossipTimeoutMsDefault;
			IndexCacheDepth = Opts.IndexCacheDepthDefault;
			SkipIndexVerify = Opts.SkipIndexVerifyDefault;
			OptimizeIndexMerge = Opts.OptimizeIndexMergeDefault;
			EnableHistograms = Opts.HistogramEnabledDefault;
			ReaderThreadsCount = Opts.ReaderThreadsCountDefault;

			StartStandardProjections = Opts.StartStandardProjectionsDefault;
			DisableHTTPCaching = Opts.DisableHttpCachingDefault;
			LogHttpRequests = Opts.LogHttpRequestsDefault;

			Unbuffered = Opts.UnbufferedDefault;
			WriteThrough = Opts.WriteThroughDefault;

			AlwaysKeepScavenged = Opts.AlwaysKeepScavengedDefault;

			SkipIndexScanOnReads = Opts.SkipIndexScanOnReadsDefault;
			ReduceFileCachePressure = Opts.ReduceFileCachePressureDefault;
			InitializationThreads = Opts.InitializationThreadsDefault;
			StructuredLog = Opts.StructuredLogDefault;

			ConnectionPendingSendBytesThreshold = Opts.ConnectionPendingSendBytesThresholdDefault;
			ChunkInitialReaderCount = Opts.ChunkInitialReaderCountDefault;

			MaxAutoMergeIndexLevel = Opts.MaxAutoMergeIndexLevelDefault;
		}
	}
}
