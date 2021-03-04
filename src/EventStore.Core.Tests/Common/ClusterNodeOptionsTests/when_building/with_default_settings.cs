using System.Net;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Authentication.DelegatedAuthentication;
using EventStore.Core.Services.Monitoring;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Util;
using NUnit.Framework;

namespace EventStore.Core.Tests.Common.ClusterNodeOptionsTests.when_building {
	[TestFixture]
	public class with_default_node_as_single_node : SingleNodeScenario {
		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) => options;

		[Test]
		public void should_create_single_cluster_node() {
			Assert.IsNotNull(_node);
			Assert.AreEqual(1, _options.Cluster.ClusterSize, "ClusterNodeCount");
			Assert.IsInstanceOf<DelegatedAuthenticationProvider>(_node.AuthenticationProvider);
			Assert.AreEqual(StatsStorage.File, _options.Database.StatsStorage);
		}

		[Test]
		public void should_have_default_endpoints() {
			Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 1112), _node.NodeInfo.InternalSecureTcp);
			Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 1113), _node.NodeInfo.ExternalSecureTcp);
			Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 2113), _node.NodeInfo.HttpEndPoint);
		}

		[Test]
		public void should_use_tls() {
			Assert.IsFalse(_options.Interface.DisableInternalTcpTls);
			Assert.IsFalse(_options.Interface.DisableExternalTcpTls);
		}

		[Test]
		public void should_set_command_line_args_to_default_values() {
			Assert.AreEqual(Opts.EnableTrustedAuthDefault, _options.Interface.EnableTrustedAuth, "EnableTrustedAuth");
			Assert.AreEqual(Opts.LogHttpRequestsDefault, _options.Application.LogHttpRequests, "LogHttpRequests");
			Assert.AreEqual(Opts.WorkerThreadsDefault, _options.Application.WorkerThreads, "WorkerThreads");
			Assert.AreEqual(Opts.DiscoverViaDnsDefault, _options.Cluster.DiscoverViaDns, "DiscoverViaDns");
			Assert.AreEqual(Opts.StatsPeriodDefault, _options.Application.StatsPeriodSec, "StatsPeriod");
			Assert.AreEqual(Opts.HistogramEnabledDefault, _options.Application.EnableHistograms, "EnableHistograms");
			Assert.AreEqual(Opts.DisableHttpCachingDefault, _options.Application.DisableHttpCaching, "DisableHTTPCaching");
			Assert.AreEqual(Opts.SkipDbVerifyDefault, _options.Database.SkipDbVerify, "VerifyDbHash");
			Assert.AreEqual(Opts.MinFlushDelayMsDefault, _options.Database.MinFlushDelayMs, "MinFlushDelay");
			Assert.AreEqual(Opts.ScavengeHistoryMaxAgeDefault, _options.Database.ScavengeHistoryMaxAge,
				"ScavengeHistoryMaxAge");
			Assert.AreEqual(Opts.DisableScavengeMergeDefault, _options.Database.DisableScavengeMerging,
				"DisableScavengeMerging");
			Assert.AreEqual(Opts.DisableAdminUiDefault, _options.Interface.DisableAdminUi, "AdminOnPublic");
			Assert.AreEqual(Opts.DisableStatsOnHttpDefault, _options.Interface.DisableStatsOnHttp, "StatsOnPublic");
			Assert.AreEqual(Opts.DisableGossipOnHttpDefault, _options.Interface.DisableGossipOnHttp, "GossipOnPublic");
			Assert.AreEqual(Opts.MaxMemtableSizeDefault, _options.Database.MaxMemTableSize, "MaxMemtableEntryCount");
			Assert.AreEqual(Opts.StartStandardProjectionsDefault, _options.Projections.RunProjections > ProjectionType.System,
				"StartStandardProjections");
			Assert.AreEqual(Opts.UnsafeIgnoreHardDeleteDefault, _options.Database.UnsafeIgnoreHardDelete,
				"UnsafeIgnoreHardDeletes");
			Assert.That(string.IsNullOrEmpty(_options.Database.Index), "IndexPath");
			Assert.AreEqual(1, _options.Cluster.PrepareAckCount, "PrepareAckCount");
			Assert.AreEqual(1, _options.Cluster.CommitAckCount, "CommitAckCount");
			Assert.AreEqual(Opts.PrepareTimeoutMsDefault, _options.Database.PrepareTimeoutMs, "PrepareTimeout");
			Assert.AreEqual(Opts.CommitTimeoutMsDefault, _options.Database.CommitTimeoutMs, "CommitTimeout");
			Assert.AreEqual(Opts.WriteTimeoutMsDefault, _options.Database.WriteTimeoutMs, "WriteTimeout");

			Assert.AreEqual(Opts.IntTcpHeartbeatIntervalDefault, _options.Interface.IntTcpHeartbeatInterval,
				"IntTcpHeartbeatInterval");
			Assert.AreEqual(Opts.IntTcpHeartbeatTimeoutDefault, _options.Interface.IntTcpHeartbeatTimeout,
				"IntTcpHeartbeatTimeout");
			Assert.AreEqual(Opts.ExtTcpHeartbeatIntervalDefault, _options.Interface.ExtTcpHeartbeatInterval,
				"ExtTcpHeartbeatInterval");
			Assert.AreEqual(Opts.ExtTcpHeartbeatTimeoutDefault, _options.Interface.ExtTcpHeartbeatTimeout,
				"ExtTcpHeartbeatTimeout");

			Assert.AreEqual(TFConsts.ChunkSize, _node.Db.Config.ChunkSize, "ChunkSize");
			Assert.AreEqual(Opts.ChunksCacheSizeDefault, _node.Db.Config.MaxChunksCacheSize, "MaxChunksCacheSize");
		}
	}

	[TestFixture]
	public class with_default_node_as_node_in_a_cluster : ClusterMemberScenario {
		[Test]
		public void should_create_single_cluster_node() {
			Assert.IsNotNull(_node);
			Assert.AreEqual(_clusterSize, _options.Cluster.ClusterSize, "ClusterNodeCount");
			Assert.IsInstanceOf<DelegatedAuthenticationProvider>(_node.AuthenticationProvider);
			Assert.AreEqual(StatsStorage.File, _options.Database.StatsStorage);
		}

		[Test]
		public void should_have_default_secure_endpoints() {
			var internalTcp = new IPEndPoint(IPAddress.Loopback, 1112);
			var externalTcp = new IPEndPoint(IPAddress.Loopback, 1113);
			var httpEndPoint = new IPEndPoint(IPAddress.Loopback, 2113);

			Assert.AreEqual(internalTcp, _node.NodeInfo.InternalSecureTcp);
			Assert.AreEqual(externalTcp, _node.NodeInfo.ExternalSecureTcp);
			Assert.AreEqual(httpEndPoint, _node.NodeInfo.HttpEndPoint);

			Assert.AreEqual(internalTcp.ToDnsEndPoint(), _node.GossipAdvertiseInfo.InternalSecureTcp);
			Assert.AreEqual(externalTcp.ToDnsEndPoint(), _node.GossipAdvertiseInfo.ExternalSecureTcp);
			Assert.AreEqual(httpEndPoint.ToDnsEndPoint(), _node.GossipAdvertiseInfo.HttpEndPoint);
		}

		[Test]
		public void should_use_tls() {
			Assert.IsFalse(_options.Interface.DisableInternalTcpTls);
			Assert.IsFalse(_options.Interface.DisableExternalTcpTls);
		}

		[Test]
		public void should_set_commit_and_prepare_counts_to_quorum_size() {
			var quorumSize = _clusterSize / 2 + 1;
			Assert.AreEqual(quorumSize, _options.Cluster.PrepareAckCount, "PrepareAckCount");
			Assert.AreEqual(quorumSize, _options.Cluster.CommitAckCount, "CommitAckCount");
		}

		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) => options;
	}

	[TestFixture]
	public class with_default_node_as_node_in_an_insecure_cluster : ClusterMemberScenario {
		[Test]
		public void should_create_single_cluster_node() {
			Assert.IsNotNull(_node);
			Assert.AreEqual(_clusterSize, _options.Cluster.ClusterSize, "ClusterNodeCount");
			Assert.IsInstanceOf<DelegatedAuthenticationProvider>(_node.AuthenticationProvider);
			Assert.AreEqual(StatsStorage.File, _options.Database.StatsStorage);
		}

		[Test]
		public void should_have_default_endpoints() {
			var internalTcp = new IPEndPoint(IPAddress.Loopback, 1112);
			var externalTcp = new IPEndPoint(IPAddress.Loopback, 1113);
			var httpEndPoint = new IPEndPoint(IPAddress.Loopback, 2113);

			Assert.AreEqual(internalTcp, _node.NodeInfo.InternalTcp);
			Assert.AreEqual(externalTcp, _node.NodeInfo.ExternalTcp);
			Assert.AreEqual(httpEndPoint, _node.NodeInfo.HttpEndPoint);

			Assert.AreEqual(internalTcp.ToDnsEndPoint(), _node.GossipAdvertiseInfo.InternalTcp);
			Assert.AreEqual(externalTcp.ToDnsEndPoint(), _node.GossipAdvertiseInfo.ExternalTcp);
			Assert.AreEqual(httpEndPoint.ToDnsEndPoint(), _node.GossipAdvertiseInfo.HttpEndPoint);
		}

		[Test]
		public void should_set_commit_and_prepare_counts_to_quorum_size() {
			var quorumSize = _clusterSize / 2 + 1;
			Assert.AreEqual(quorumSize, _options.Cluster.PrepareAckCount, "PrepareAckCount");
			Assert.AreEqual(quorumSize, _options.Cluster.CommitAckCount, "CommitAckCount");
		}

		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) => options with {
			Application = options.Application with {
				Insecure = true
			}
		};
	}

}
