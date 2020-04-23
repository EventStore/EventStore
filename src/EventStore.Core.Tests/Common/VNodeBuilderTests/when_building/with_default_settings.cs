using EventStore.Core.Services.Monitoring;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Util;
using NUnit.Framework;
using System.Net;
using EventStore.Core.Authentication;

namespace EventStore.Core.Tests.Common.VNodeBuilderTests.when_building {
	[TestFixture]
	public class with_default_settings_as_single_node : SingleNodeScenario {
		public override void Given() {
		}

		[Test]
		public void should_create_single_cluster_node() {
			Assert.IsNotNull(_node);
			Assert.AreEqual(1, _settings.ClusterNodeCount, "ClusterNodeCount");
			Assert.IsInstanceOf<AuthenticationProviderFactory>(_settings.AuthenticationProviderFactory);
			Assert.AreEqual(StatsStorage.Stream, _settings.StatsStorage);
		}

		[Test]
		public void should_have_default_endpoints() {
			Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 1112), _settings.NodeInfo.InternalTcp);
			Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 1113), _settings.NodeInfo.ExternalTcp);
			Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 2112), _settings.NodeInfo.InternalHttp);
			Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 2113), _settings.NodeInfo.ExternalHttp);
		}

		[Test]
		public void should_use_tls() {
			Assert.IsFalse(_settings.DisableInternalTls);
			Assert.IsFalse(_settings.DisableExternalTls);
		}

		[Test]
		public void should_set_command_line_args_to_default_values() {
			Assert.AreEqual(Opts.EnableTrustedAuthDefault, _settings.EnableTrustedAuth, "EnableTrustedAuth");
			Assert.AreEqual(Opts.LogHttpRequestsDefault, _settings.LogHttpRequests, "LogHttpRequests");
			Assert.AreEqual(Opts.WorkerThreadsDefault, _settings.WorkerThreads, "WorkerThreads");
			Assert.AreEqual(Opts.DiscoverViaDnsDefault, _settings.DiscoverViaDns, "DiscoverViaDns");
			Assert.AreEqual(Opts.StatsPeriodDefault, _settings.StatsPeriod.Seconds, "StatsPeriod");
			Assert.AreEqual(Opts.HistogramEnabledDefault, _settings.EnableHistograms, "EnableHistograms");
			Assert.AreEqual(Opts.DisableHttpCachingDefault, _settings.DisableHTTPCaching, "DisableHTTPCaching");
			Assert.AreEqual(Opts.SkipDbVerifyDefault, !_settings.VerifyDbHash, "VerifyDbHash");
			Assert.AreEqual(Opts.MinFlushDelayMsDefault, _settings.MinFlushDelay.TotalMilliseconds, "MinFlushDelay");
			Assert.AreEqual(Opts.ScavengeHistoryMaxAgeDefault, _settings.ScavengeHistoryMaxAge,
				"ScavengeHistoryMaxAge");
			Assert.AreEqual(Opts.DisableScavengeMergeDefault, _settings.DisableScavengeMerging,
				"DisableScavengeMerging");
			Assert.AreEqual(Opts.AdminOnExtDefault, _settings.AdminOnPublic, "AdminOnPublic");
			Assert.AreEqual(Opts.StatsOnExtDefault, _settings.StatsOnPublic, "StatsOnPublic");
			Assert.AreEqual(Opts.GossipOnExtDefault, _settings.GossipOnPublic, "GossipOnPublic");
			Assert.AreEqual(Opts.MaxMemtableSizeDefault, _settings.MaxMemtableEntryCount, "MaxMemtableEntryCount");
			Assert.AreEqual(Opts.StartStandardProjectionsDefault, _settings.StartStandardProjections,
				"StartStandardProjections");
			Assert.AreEqual(Opts.UnsafeIgnoreHardDeleteDefault, _settings.UnsafeIgnoreHardDeletes,
				"UnsafeIgnoreHardDeletes");
			Assert.That(string.IsNullOrEmpty(_settings.Index), "IndexPath");
			Assert.AreEqual(1, _settings.PrepareAckCount, "PrepareAckCount");
			Assert.AreEqual(1, _settings.CommitAckCount, "CommitAckCount");
			Assert.AreEqual(Opts.PrepareTimeoutMsDefault, _settings.PrepareTimeout.TotalMilliseconds, "PrepareTimeout");
			Assert.AreEqual(Opts.CommitTimeoutMsDefault, _settings.CommitTimeout.TotalMilliseconds, "CommitTimeout");
			Assert.AreEqual(Opts.WriteTimeoutMsDefault, _settings.WriteTimeout.TotalMilliseconds, "WriteTimeout");

			Assert.AreEqual(Opts.IntTcpHeartbeatIntervalDefault, _settings.IntTcpHeartbeatInterval.TotalMilliseconds,
				"IntTcpHeartbeatInterval");
			Assert.AreEqual(Opts.IntTcpHeartbeatTimeoutDefault, _settings.IntTcpHeartbeatTimeout.TotalMilliseconds,
				"IntTcpHeartbeatTimeout");
			Assert.AreEqual(Opts.ExtTcpHeartbeatIntervalDefault, _settings.ExtTcpHeartbeatInterval.TotalMilliseconds,
				"ExtTcpHeartbeatInterval");
			Assert.AreEqual(Opts.ExtTcpHeartbeatTimeoutDefault, _settings.ExtTcpHeartbeatTimeout.TotalMilliseconds,
				"ExtTcpHeartbeatTimeout");

			Assert.AreEqual(TFConsts.ChunkSize, _dbConfig.ChunkSize, "ChunkSize");
			Assert.AreEqual(Opts.ChunksCacheSizeDefault, _dbConfig.MaxChunksCacheSize, "MaxChunksCacheSize");
		}
	}

	[TestFixture]
	public class with_default_settings_as_node_in_a_cluster : ClusterMemberScenario {
		public override void Given() {
		}

		[Test]
		public void should_create_single_cluster_node() {
			Assert.IsNotNull(_node);
			Assert.AreEqual(_clusterSize, _settings.ClusterNodeCount, "ClusterNodeCount");
			Assert.IsInstanceOf<AuthenticationProviderFactory>(_settings.AuthenticationProviderFactory);
			Assert.AreEqual(StatsStorage.Stream, _settings.StatsStorage);
		}

		[Test]
		public void should_have_default_endpoints() {
			var internalTcp = new IPEndPoint(IPAddress.Loopback, 1112);
			var externalTcp = new IPEndPoint(IPAddress.Loopback, 1113);
			var internalHttp = new IPEndPoint(IPAddress.Loopback, 2112);
			var externalHttp = new IPEndPoint(IPAddress.Loopback, 2113);

			Assert.AreEqual(internalTcp, _settings.NodeInfo.InternalTcp);
			Assert.AreEqual(externalTcp, _settings.NodeInfo.ExternalTcp);
			Assert.AreEqual(internalHttp, _settings.NodeInfo.InternalHttp);
			Assert.AreEqual(externalHttp, _settings.NodeInfo.ExternalHttp);

			Assert.AreEqual(internalTcp, _settings.GossipAdvertiseInfo.InternalTcp);
			Assert.AreEqual(externalTcp, _settings.GossipAdvertiseInfo.ExternalTcp);
			Assert.AreEqual(internalHttp, _settings.GossipAdvertiseInfo.InternalHttp);
			Assert.AreEqual(externalHttp, _settings.GossipAdvertiseInfo.ExternalHttp);
		}

		[Test]
		public void should_use_tls() {
			Assert.IsFalse(_settings.DisableInternalTls);
			Assert.IsFalse(_settings.DisableExternalTls);
		}

		[Test]
		public void should_set_command_line_args_to_default_values() {
			Assert.AreEqual(Opts.EnableTrustedAuthDefault, _settings.EnableTrustedAuth, "EnableTrustedAuth");
			Assert.AreEqual(Opts.LogHttpRequestsDefault, _settings.LogHttpRequests, "LogHttpRequests");
			Assert.AreEqual(Opts.WorkerThreadsDefault, _settings.WorkerThreads, "WorkerThreads");
			Assert.AreEqual(Opts.DiscoverViaDnsDefault, _settings.DiscoverViaDns, "DiscoverViaDns");
			Assert.AreEqual(Opts.StatsPeriodDefault, _settings.StatsPeriod.Seconds, "StatsPeriod");
			Assert.AreEqual(Opts.HistogramEnabledDefault, _settings.EnableHistograms, "EnableHistograms");
			Assert.AreEqual(Opts.DisableHttpCachingDefault, _settings.DisableHTTPCaching, "DisableHTTPCaching");
			Assert.AreEqual(Opts.SkipDbVerifyDefault, !_settings.VerifyDbHash, "VerifyDbHash");
			Assert.AreEqual(Opts.MinFlushDelayMsDefault, _settings.MinFlushDelay.TotalMilliseconds, "MinFlushDelay");
			Assert.AreEqual(Opts.ScavengeHistoryMaxAgeDefault, _settings.ScavengeHistoryMaxAge,
				"ScavengeHistoryMaxAge");
			Assert.AreEqual(Opts.DisableScavengeMergeDefault, _settings.DisableScavengeMerging,
				"DisableScavengeMerging");
			Assert.AreEqual(Opts.AdminOnExtDefault, _settings.AdminOnPublic, "AdminOnPublic");
			Assert.AreEqual(Opts.StatsOnExtDefault, _settings.StatsOnPublic, "StatsOnPublic");
			Assert.AreEqual(Opts.GossipOnExtDefault, _settings.GossipOnPublic, "GossipOnPublic");
			Assert.AreEqual(Opts.MaxMemtableSizeDefault, _settings.MaxMemtableEntryCount, "MaxMemtableEntryCount");
			Assert.AreEqual(Opts.StartStandardProjectionsDefault, _settings.StartStandardProjections,
				"StartStandardProjections");
			Assert.AreEqual(Opts.UnsafeIgnoreHardDeleteDefault, _settings.UnsafeIgnoreHardDeletes,
				"UnsafeIgnoreHardDeletes");
			Assert.That(string.IsNullOrEmpty(_settings.Index), "IndexPath");
			Assert.AreEqual(Opts.PrepareTimeoutMsDefault, _settings.PrepareTimeout.TotalMilliseconds, "PrepareTimeout");
			Assert.AreEqual(Opts.CommitTimeoutMsDefault, _settings.CommitTimeout.TotalMilliseconds, "CommitTimeout");

			Assert.AreEqual(Opts.IntTcpHeartbeatIntervalDefault, _settings.IntTcpHeartbeatInterval.TotalMilliseconds,
				"IntTcpHeartbeatInterval");
			Assert.AreEqual(Opts.IntTcpHeartbeatTimeoutDefault, _settings.IntTcpHeartbeatTimeout.TotalMilliseconds,
				"IntTcpHeartbeatTimeout");
			Assert.AreEqual(Opts.ExtTcpHeartbeatIntervalDefault, _settings.ExtTcpHeartbeatInterval.TotalMilliseconds,
				"ExtTcpHeartbeatInterval");
			Assert.AreEqual(Opts.ExtTcpHeartbeatTimeoutDefault, _settings.ExtTcpHeartbeatTimeout.TotalMilliseconds,
				"ExtTcpHeartbeatTimeout");

			Assert.AreEqual(TFConsts.ChunkSize, _dbConfig.ChunkSize, "ChunkSize");
			Assert.AreEqual(Opts.ChunksCacheSizeDefault, _dbConfig.MaxChunksCacheSize, "MaxChunksCacheSize");
			Assert.AreEqual(Opts.AlwaysKeepScavengedDefault, _settings.AlwaysKeepScavenged, "AlwaysKeepScavenged");
		}

		[Test]
		public void should_set_commit_and_prepare_counts_to_quorum_size() {
			var quorumSize = _clusterSize / 2 + 1;
			Assert.AreEqual(quorumSize, _settings.PrepareAckCount, "PrepareAckCount");
			Assert.AreEqual(quorumSize, _settings.CommitAckCount, "CommitAckCount");
		}
	}
}
