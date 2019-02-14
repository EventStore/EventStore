using NUnit.Framework;
using System;
using System.IO;
using System.Net;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Services.Monitoring;
using System.Collections.Generic;
using EventStore.Common.Utils;

namespace EventStore.Core.Tests.Common.VNodeBuilderTests.when_building {
	[TestFixture]
	public class with_run_on_disk : SingleNodeScenario {
		private string _dbPath;

		public override void Given() {
			_dbPath = Path.Combine(Path.GetTempPath(), string.Format("Test-{0}", Guid.NewGuid()));
			_builder.RunOnDisk(_dbPath);
		}

		[Test]
		public void should_set_memdb_to_false() {
			Assert.IsFalse(_dbConfig.InMemDb);
		}

		[Test]
		public void should_set_the_db_path() {
			Assert.AreEqual(_dbPath, _dbConfig.Path);
		}
	}

	[TestFixture]
	public class with_default_endpoints_option : SingleNodeScenario {
		public override void Given() {
			var noEndpoint = new IPEndPoint(IPAddress.None, 0);
			_builder.WithInternalHttpOn(noEndpoint)
				.WithExternalHttpOn(noEndpoint)
				.WithInternalTcpOn(noEndpoint)
				.WithExternalTcpOn(noEndpoint);

			_builder.OnDefaultEndpoints();
		}

		[Test]
		public void should_set_internal_tcp() {
			Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 1112), _settings.NodeInfo.InternalTcp);
		}

		[Test]
		public void should_set_external_tcp() {
			Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 1113), _settings.NodeInfo.ExternalTcp);
		}

		[Test]
		public void should_set_internal_http() {
			Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 2112), _settings.NodeInfo.InternalHttp);
		}

		[Test]
		public void should_set_external_http() {
			Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 2113), _settings.NodeInfo.ExternalHttp);
		}
	}

	[TestFixture]
	public class with_run_in_memory : SingleNodeScenario {
		public override void Given() {
			_builder.RunInMemory();
		}

		[Test]
		public void should_set_memdb_to_true() {
			Assert.IsTrue(_dbConfig.InMemDb);
		}
	}

	[TestFixture]
	public class with_log_http_requests_enabled : SingleNodeScenario {
		public override void Given() {
			_builder.EnableLoggingOfHttpRequests();
		}

		[Test]
		public void should_set_http_logging_to_true() {
			Assert.IsTrue(_settings.LogHttpRequests);
		}
	}

	[TestFixture]
	public class with_custom_number_of_worker_threads : SingleNodeScenario {
		public override void Given() {
			_builder.WithWorkerThreads(10);
		}

		[Test]
		public void should_set_the_number_of_worker_threads() {
			Assert.AreEqual(10, _settings.WorkerThreads);
		}
	}

	[TestFixture]
	public class with_custom_stats_period : SingleNodeScenario {
		public override void Given() {
			_builder.WithStatsPeriod(TimeSpan.FromSeconds(1));
		}

		[Test]
		public void should_set_the_stats_period() {
			Assert.AreEqual(1, _settings.StatsPeriod.TotalSeconds);
		}
	}

	[TestFixture]
	public class with_custom_stats_storage : SingleNodeScenario {
		public override void Given() {
			_builder.WithStatsStorage(StatsStorage.None);
		}

		[Test]
		public void should_set_the_stats_storage() {
			Assert.AreEqual(StatsStorage.None, _settings.StatsStorage);
		}
	}

	[TestFixture]
	public class with_histograms_enabled : SingleNodeScenario {
		public override void Given() {
			_builder.EnableHistograms();
		}

		[Test]
		public void should_enable_histograms() {
			Assert.IsTrue(_settings.EnableHistograms);
		}
	}

	[TestFixture]
	public class with_trusted_auth_enabled : SingleNodeScenario {
		public override void Given() {
			_builder.EnableTrustedAuth();
		}

		[Test]
		public void should_enable_trusted_authentication() {
			Assert.IsTrue(_settings.EnableTrustedAuth);
		}
	}

	[TestFixture]
	public class with_http_caching_disabled : SingleNodeScenario {
		public override void Given() {
			_builder.DisableHTTPCaching();
		}

		[Test]
		public void should_disable_http_caching() {
			Assert.IsTrue(_settings.DisableHTTPCaching);
		}
	}

	[TestFixture]
	public class without_verifying_db_hashes : SingleNodeScenario {
		public override void Given() {
			_builder.DoNotVerifyDbHashes();
		}

		[Test]
		public void should_set_verify_db_hashes_to_false() {
			Assert.IsFalse(_settings.VerifyDbHash);
		}
	}

	[TestFixture]
	public class with_verifying_db_hashes : SingleNodeScenario {
		public override void Given() {
			_builder.DoNotVerifyDbHashes() // Turn off verification before turning it back on
				.VerifyDbHashes();
		}

		[Test]
		public void should_set_verify_db_hashes_to_true() {
			Assert.IsTrue(_settings.VerifyDbHash);
		}
	}

	[TestFixture]
	public class with_custom_min_flush_delay : SingleNodeScenario {
		public override void Given() {
			_builder.WithMinFlushDelay(TimeSpan.FromMilliseconds(1200));
		}

		[Test]
		public void should_set_the_min_flush_delay() {
			Assert.AreEqual(1200, _settings.MinFlushDelay.TotalMilliseconds);
		}
	}

	[TestFixture]
	public class with_custom_scavenge_history_max_age : SingleNodeScenario {
		public override void Given() {
			_builder.WithScavengeHistoryMaxAge(2);
		}

		[Test]
		public void should_set_the_scavenge_history_max_age() {
			Assert.AreEqual(2, _settings.ScavengeHistoryMaxAge);
		}
	}

	[TestFixture]
	public class with_scavenge_merging_disabled : SingleNodeScenario {
		public override void Given() {
			_builder.DisableScavengeMerging();
		}

		[Test]
		public void should_disable_scavenge_merging() {
			Assert.IsTrue(_settings.DisableScavengeMerging);
		}
	}

	[TestFixture]
	public class with_custom_max_memtable_size : SingleNodeScenario {
		public override void Given() {
			_builder.MaximumMemoryTableSizeOf(200);
		}

		[Test]
		public void should_set_the_max_memtable_size() {
			Assert.AreEqual(200, _settings.MaxMemtableEntryCount);
		}
	}

	[TestFixture]
	public class with_custom_hash_collision_read_limit : SingleNodeScenario {
		public override void Given() {
			_builder.WithHashCollisionReadLimitOf(200);
		}

		[Test]
		public void should_set_the_hash_collision_read_limit() {
			Assert.AreEqual(200, _settings.HashCollisionReadLimit);
		}
	}

	[TestFixture]
	public class with_standard_projections_started : SingleNodeScenario {
		public override void Given() {
			_builder.StartStandardProjections();
		}

		[Test]
		public void should_set_start_standard_projections_to_true() {
			Assert.IsTrue(_settings.StartStandardProjections);
		}
	}

	[TestFixture]
	public class with_ignore_hard_delete_enabled : SingleNodeScenario {
		public override void Given() {
			_builder.WithUnsafeIgnoreHardDelete();
		}

		[Test]
		public void should_set_ignore_hard_deletes() {
			Assert.IsTrue(_settings.UnsafeIgnoreHardDeletes);
		}
	}

	[TestFixture]
	public class with_better_ordering_enabled : SingleNodeScenario {
		public override void Given() {
			_builder.WithBetterOrdering();
		}

		[Test]
		public void should_set_better_ordering() {
			Assert.IsTrue(_settings.BetterOrdering);
		}
	}

	[TestFixture]
	public class with_custom_index_path : SingleNodeScenario {
		public override void Given() {
			_builder.WithIndexPath("index");
		}

		[Test]
		public void should_set_the_index_path() {
			Assert.AreEqual("index", _settings.Index);
		}
	}

	[TestFixture]
	public class with_custom_prepare_timeout : SingleNodeScenario {
		public override void Given() {
			_builder.WithPrepareTimeout(TimeSpan.FromMilliseconds(1234));
		}

		[Test]
		public void should_set_the_prepare_timeout() {
			Assert.AreEqual(1234, _settings.PrepareTimeout.TotalMilliseconds);
		}
	}

	[TestFixture]
	public class with_custom_commit_timeout : SingleNodeScenario {
		public override void Given() {
			_builder.WithCommitTimeout(TimeSpan.FromMilliseconds(1234));
		}

		[Test]
		public void should_set_the_commit_timeout() {
			Assert.AreEqual(1234, _settings.CommitTimeout.TotalMilliseconds);
		}
	}

	[TestFixture]
	public class with_custom_internal_heartbeat_interval : SingleNodeScenario {
		public override void Given() {
			_builder.WithInternalHeartbeatInterval(TimeSpan.FromMilliseconds(1234));
		}

		[Test]
		public void should_set_the_internal_heartbeat_interval() {
			Assert.AreEqual(1234, _settings.IntTcpHeartbeatInterval.TotalMilliseconds);
		}
	}

	[TestFixture]
	public class with_custom_internal_heartbeat_timeout : SingleNodeScenario {
		public override void Given() {
			_builder.WithInternalHeartbeatTimeout(TimeSpan.FromMilliseconds(1234));
		}

		[Test]
		public void should_set_the_internal_heartbeat_timeout() {
			Assert.AreEqual(1234, _settings.IntTcpHeartbeatTimeout.TotalMilliseconds);
		}
	}

	[TestFixture]
	public class with_custom_external_heartbeat_interval : SingleNodeScenario {
		public override void Given() {
			_builder.WithExternalHeartbeatInterval(TimeSpan.FromMilliseconds(1234));
		}

		[Test]
		public void should_set_the_external_heartbeat_interval() {
			Assert.AreEqual(1234, _settings.ExtTcpHeartbeatInterval.TotalMilliseconds);
		}
	}

	[TestFixture]
	public class with_custom_external_heartbeat_timeout : SingleNodeScenario {
		public override void Given() {
			_builder.WithExternalHeartbeatTimeout(TimeSpan.FromMilliseconds(1234));
		}

		[Test]
		public void should_set_the_external_heartbeat_timeout() {
			Assert.AreEqual(1234, _settings.ExtTcpHeartbeatTimeout.TotalMilliseconds);
		}
	}

	[TestFixture]
	public class with_no_admin_on_public_interface : SingleNodeScenario {
		public override void Given() {
			_builder.NoAdminOnPublicInterface();
		}

		[Test]
		public void should_disable_admin_on_public() {
			Assert.IsFalse(_settings.AdminOnPublic);
		}
	}

	[TestFixture]
	public class with_no_gossip_on_public_interface : SingleNodeScenario {
		public override void Given() {
			_builder.NoGossipOnPublicInterface();
		}

		[Test]
		public void should_disable_gossip_on_public() {
			Assert.IsFalse(_settings.GossipOnPublic);
		}
	}

	[TestFixture]
	public class with_no_stats_on_public_interface : SingleNodeScenario {
		public override void Given() {
			_builder.NoStatsOnPublicInterface();
		}

		[Test]
		public void should_disable_stats_on_public() {
			Assert.IsFalse(_settings.StatsOnPublic);
		}
	}

	[TestFixture]
	public class with_custom_ip_endpoints : SingleNodeScenario {
		private IPEndPoint _internalHttp;
		private IPEndPoint _externalHttp;
		private IPEndPoint _internalTcp;
		private IPEndPoint _externalTcp;

		public override void Given() {
			var baseIpAddress = IPAddress.Parse("192.168.1.15");
			_internalHttp = new IPEndPoint(baseIpAddress, 1112);
			_externalHttp = new IPEndPoint(baseIpAddress, 1113);
			_internalTcp = new IPEndPoint(baseIpAddress, 1114);
			_externalTcp = new IPEndPoint(baseIpAddress, 1115);
			_builder.WithInternalHttpOn(_internalHttp)
				.WithExternalHttpOn(_externalHttp)
				.WithExternalTcpOn(_externalTcp)
				.WithInternalTcpOn(_internalTcp);
		}

		[Test]
		public void should_set_internal_http_endpoint() {
			Assert.AreEqual(_internalHttp, _settings.NodeInfo.InternalHttp);
		}

		[Test]
		public void should_set_external_http_endpoint() {
			Assert.AreEqual(_externalHttp, _settings.NodeInfo.ExternalHttp);
		}

		[Test]
		public void should_set_internal_tcp_endpoint() {
			Assert.AreEqual(_internalTcp, _settings.NodeInfo.InternalTcp);
		}

		[Test]
		public void should_set_external_tcp_endpoint() {
			Assert.AreEqual(_externalTcp, _settings.NodeInfo.ExternalTcp);
		}

		[Test]
		public void should_set_internal_http_prefixes() {
			var internalHttpPrefix = string.Format("http://{0}/", _internalHttp);
			CollectionAssert.AreEqual(new string[] {internalHttpPrefix}, _settings.IntHttpPrefixes);
		}

		[Test]
		public void should_set_external_http_prefixes() {
			var externalHttpPrefix = string.Format("http://{0}/", _externalHttp);
			CollectionAssert.AreEqual(new string[] {externalHttpPrefix}, _settings.ExtHttpPrefixes);
		}
	}

	[TestFixture]
	public class with_custom_http_prefixes : SingleNodeScenario {
		private string _intPrefix;
		private string _intLoopbackPrefix;
		private string _extPrefix;
		private string _extLoopbackPrefix;

		public override void Given() {
			var baseIpAddress = IPAddress.Parse("192.168.1.15");
			int intPort = 1112;
			int extPort = 1113;

			var internalHttp = new IPEndPoint(baseIpAddress, intPort);
			var externalHttp = new IPEndPoint(baseIpAddress, extPort);

			_intPrefix = string.Format("http://{0}/", internalHttp);
			_intLoopbackPrefix = string.Format("http://{0}/", new IPEndPoint(IPAddress.Loopback, intPort));
			_extPrefix = string.Format("http://{0}/", externalHttp);
			_extLoopbackPrefix = string.Format("http://{0}/", new IPEndPoint(IPAddress.Loopback, extPort));

			_builder.WithInternalHttpOn(internalHttp)
				.WithExternalHttpOn(externalHttp)
				.AddInternalHttpPrefix(_intPrefix)
				.AddInternalHttpPrefix(_intLoopbackPrefix)
				.AddExternalHttpPrefix(_extPrefix)
				.AddExternalHttpPrefix(_extLoopbackPrefix);
		}

		[Test]
		public void should_set_internal_http_prefixes() {
			CollectionAssert.AreEqual(new string[] {_intPrefix, _intLoopbackPrefix}, _settings.IntHttpPrefixes);
		}

		[Test]
		public void should_set_external_http_prefixes() {
			CollectionAssert.AreEqual(new string[] {_extPrefix, _extLoopbackPrefix}, _settings.ExtHttpPrefixes);
		}
	}

	[TestFixture]
	public class with_add_interface_prefixes : SingleNodeScenario {
		private IPEndPoint _internalHttp;
		private IPEndPoint _externalHttp;
		private IPEndPoint _internalTcp;
		private IPEndPoint _externalTcp;

		public override void Given() {
			var baseIpAddress = IPAddress.Loopback;
			_internalHttp = new IPEndPoint(baseIpAddress, 1112);
			_externalHttp = new IPEndPoint(baseIpAddress, 1113);
			_internalTcp = new IPEndPoint(baseIpAddress, 1114);
			_externalTcp = new IPEndPoint(baseIpAddress, 1115);
			_builder.WithInternalHttpOn(_internalHttp)
				.WithExternalHttpOn(_externalHttp)
				.WithExternalTcpOn(_externalTcp)
				.WithInternalTcpOn(_internalTcp);
		}

		[Test]
		public void should_set_internal_http_prefixes() {
			var internalHttpPrefixes = new List<string> {
				string.Format("http://{0}/", _internalHttp), string.Format("http://localhost:{0}/", _internalHttp.Port)
			};
			CollectionAssert.AreEqual(internalHttpPrefixes, _settings.IntHttpPrefixes);
		}

		[Test]
		public void should_set_external_http_prefixes() {
			var externalHttpPrefixes = new List<string> {
				string.Format("http://{0}/", _externalHttp), string.Format("http://localhost:{0}/", _externalHttp.Port)
			};
			CollectionAssert.AreEqual(externalHttpPrefixes, _settings.ExtHttpPrefixes);
		}
	}

	[TestFixture]
	public class with_dont_add_interface_prefixes : SingleNodeScenario {
		private IPEndPoint _internalHttp;
		private IPEndPoint _externalHttp;
		private IPEndPoint _internalTcp;
		private IPEndPoint _externalTcp;

		public override void Given() {
			var baseIpAddress = IPAddress.Loopback;
			_internalHttp = new IPEndPoint(baseIpAddress, 1112);
			_externalHttp = new IPEndPoint(baseIpAddress, 1113);
			_internalTcp = new IPEndPoint(baseIpAddress, 1114);
			_externalTcp = new IPEndPoint(baseIpAddress, 1115);
			_builder.WithInternalHttpOn(_internalHttp)
				.WithExternalHttpOn(_externalHttp)
				.WithExternalTcpOn(_externalTcp)
				.WithInternalTcpOn(_internalTcp)
				.DontAddInterfacePrefixes();
		}

		[Test]
		public void should_set_no_internal_http_prefixes() {
			CollectionAssert.IsEmpty(_settings.IntHttpPrefixes);
		}

		[Test]
		public void should_set_no_external_http_prefixes() {
			CollectionAssert.IsEmpty(_settings.ExtHttpPrefixes);
		}
	}

	[TestFixture]
	public class with_custom_index_cache_depth : SingleNodeScenario {
		public override void Given() {
			_builder.WithIndexCacheDepth(8);
		}

		[Test]
		public void should_set_index_cache_depth() {
			Assert.AreEqual(8, _settings.IndexCacheDepth);
		}
	}

	[TestFixture]
	public class with_custom_authentication_provider_factory : SingleNodeScenario {
		public override void Given() {
			_builder.WithAuthenticationProvider(new TestAuthenticationProviderFactory());
		}

		[Test]
		public void should_set_authentication_provider_factory() {
			Assert.IsInstanceOf(typeof(TestAuthenticationProviderFactory), _settings.AuthenticationProviderFactory);
		}
	}

	[TestFixture]
	public class with_custom_chunk_size : SingleNodeScenario {
		private int _chunkSize;

		public override void Given() {
			_chunkSize = 268435712;
			_builder.WithTfChunkSize(_chunkSize);
		}

		[Test]
		public void should_set_chunk_size() {
			Assert.AreEqual(_chunkSize, _dbConfig.ChunkSize);
		}
	}

	[TestFixture]
	public class with_custom_chunk_cache_size : SingleNodeScenario {
		private long _chunkCacheSize;

		public override void Given() {
			_chunkCacheSize = 268435712;
			_builder.WithTfChunksCacheSize(_chunkCacheSize);
		}

		[Test]
		public void should_set_max_chunk_cache_size() {
			Assert.AreEqual(_chunkCacheSize, _dbConfig.MaxChunksCacheSize);
		}
	}

	[TestFixture]
	public class with_custom_number_of_cached_chunks : SingleNodeScenario {
		public override void Given() {
			_builder.WithTfCachedChunks(10);
		}

		[Test]
		public void should_set_max_chunk_size_to_the_size_of_the_number_of_cached_chunks() {
			var chunkSizeResult = 10 * (long)(TFConsts.ChunkSize + ChunkHeader.Size + ChunkFooter.Size);
			Assert.AreEqual(chunkSizeResult, _dbConfig.MaxChunksCacheSize);
		}
	}

	[TestFixture]
	public class with_custom_advertise_as : SingleNodeScenario {
		private Data.GossipAdvertiseInfo _advertiseInfo;

		public override void Given() {
			var internalIPToAdvertise = IPAddress.Parse("192.168.1.1");
			var externalIPToAdvertise = IPAddress.Parse("192.168.1.2");
			var intTcpEndpoint = new IPEndPoint(internalIPToAdvertise, 1111);
			var intSecTcpEndpoint = new IPEndPoint(internalIPToAdvertise, 1112);
			var extTcpEndpoint = new IPEndPoint(externalIPToAdvertise, 1113);
			var extSecTcpEndpoint = new IPEndPoint(externalIPToAdvertise, 1114);
			var intHttpEndpoint = new IPEndPoint(internalIPToAdvertise, 1115);
			var extHttpEndpoint = new IPEndPoint(externalIPToAdvertise, 1116);

			_advertiseInfo = new Data.GossipAdvertiseInfo(intTcpEndpoint, intSecTcpEndpoint, extTcpEndpoint,
				extSecTcpEndpoint, intHttpEndpoint, extHttpEndpoint, internalIPToAdvertise, externalIPToAdvertise,
				intHttpEndpoint.Port, extHttpEndpoint.Port);

			_builder.AdvertiseInternalIPAs(internalIPToAdvertise)
				.AdvertiseExternalIPAs(externalIPToAdvertise)
				.AdvertiseInternalTCPPortAs(intTcpEndpoint.Port)
				.AdvertiseExternalTCPPortAs(extTcpEndpoint.Port)
				.AdvertiseInternalSecureTCPPortAs(intSecTcpEndpoint.Port)
				.AdvertiseExternalSecureTCPPortAs(extSecTcpEndpoint.Port)
				.AdvertiseInternalHttpPortAs(intHttpEndpoint.Port)
				.AdvertiseExternalHttpPortAs(extHttpEndpoint.Port);
		}

		[Test]
		public void should_set_the_advertise_as_info_to_the_specified() {
			Assert.AreEqual(_advertiseInfo.InternalTcp, _settings.GossipAdvertiseInfo.InternalTcp);
			Assert.AreEqual(_advertiseInfo.ExternalTcp, _settings.GossipAdvertiseInfo.ExternalTcp);
			Assert.AreEqual(_advertiseInfo.InternalSecureTcp, _settings.GossipAdvertiseInfo.InternalSecureTcp);
			Assert.AreEqual(_advertiseInfo.ExternalSecureTcp, _settings.GossipAdvertiseInfo.ExternalSecureTcp);
			Assert.AreEqual(_advertiseInfo.InternalHttp, _settings.GossipAdvertiseInfo.InternalHttp);
			Assert.AreEqual(_advertiseInfo.ExternalHttp, _settings.GossipAdvertiseInfo.ExternalHttp);
			Assert.AreEqual(_advertiseInfo.AdvertiseInternalIPAs, _settings.GossipAdvertiseInfo.AdvertiseInternalIPAs);
			Assert.AreEqual(_advertiseInfo.AdvertiseExternalIPAs, _settings.GossipAdvertiseInfo.AdvertiseExternalIPAs);
			Assert.AreEqual(_advertiseInfo.AdvertiseInternalHttpPortAs,
				_settings.GossipAdvertiseInfo.AdvertiseInternalHttpPortAs);
			Assert.AreEqual(_advertiseInfo.AdvertiseExternalHttpPortAs,
				_settings.GossipAdvertiseInfo.AdvertiseExternalHttpPortAs);
		}
	}

	[TestFixture]
	public class with_always_keep_scavenged : SingleNodeScenario {
		public override void Given() {
			_builder.AlwaysKeepScavenged();
		}

		[Test]
		public void should_always_keep_scavenged() {
			Assert.AreEqual(true, _settings.AlwaysKeepScavenged);
		}
	}

	[TestFixture]
	public class with_connection_pending_send_bytes_threshold : SingleNodeScenario {
		private int _threshold = 40 * 1024;

		public override void Given() {
			_builder.WithConnectionPendingSendBytesThreshold(_threshold);
		}

		[Test]
		public void should_set_connection_pending_send_bytes_threshold() {
			Assert.AreEqual(_threshold, _settings.ConnectionPendingSendBytesThreshold);
		}
	}
}
