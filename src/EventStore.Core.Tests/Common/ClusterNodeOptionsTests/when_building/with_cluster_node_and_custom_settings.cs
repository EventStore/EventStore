using System;
using System.Net;
using EventStore.Common.Utils;
using EventStore.Core.LogAbstraction;
using NUnit.Framework;

namespace EventStore.Core.Tests.Common.ClusterNodeOptionsTests.when_building {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class with_cluster_dns_name<TLogFormat, TStreamId> : ClusterMemberScenario<TLogFormat, TStreamId> {
		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) =>
			options with {
				Cluster = options.Cluster with {
					ClusterDns = "ClusterDns"
				}
			};

		[Test]
		public void should_set_discover_via_dns_to_true() {
			Assert.IsTrue(_options.Cluster.DiscoverViaDns);
		}

		[Test]
		public void should_set_cluster_dns_name() {
			Assert.AreEqual("ClusterDns", _options.Cluster.ClusterDns);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class with_dns_discovery_disabled_and_no_gossip_seeds<TLogFormat, TStreamId> {
		private Exception _caughtException;
		protected ClusterVNodeOptions _options;

		[OneTimeSetUp]
		public void TestFixtureSetUp() {
			_options = new ClusterVNodeOptions {
				Cluster = new() {
					DiscoverViaDns = false
				}
			}.RunInMemory();
			try {
				_ = new ClusterVNode<TStreamId>(_options, LogFormatHelper<TLogFormat, TStreamId>.LogFormat);
			} catch (Exception e) {
				_caughtException = e;
			}
		}

		[Test]
		public void should_throw_an_exception() {
			Assert.IsNotNull(_caughtException);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class with_dns_discovery_disabled_and_gossip_seeds_defined<TLogFormat, TStreamId> : ClusterMemberScenario<TLogFormat, TStreamId> {
		private EndPoint[] _gossipSeeds = {
			new DnsEndPoint("127.0.1.10", 1111),
			new DnsEndPoint("127.0.1.10", 1112),
		};

		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) =>
			options.WithGossipSeeds(_gossipSeeds);

		[Test]
		public void should_set_discover_via_dns_to_false() {
			Assert.IsFalse(_options.Cluster.DiscoverViaDns);
		}

		[Test]
		public void should_set_the_gossip_seeds() {
			CollectionAssert.AreEqual(_gossipSeeds, _options.Cluster.GossipSeed);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class with_prepare_ack_count_set_higher_than_the_quorum<TLogFormat, TStreamId> : ClusterMemberScenario<TLogFormat, TStreamId> {
		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) =>
			options with {
				Cluster = options.Cluster with {
					PrepareCount = _quorumSize + 1
				}
			};

		[Test]
		public void should_set_prepare_count_to_the_given_value() {
			Assert.AreEqual(_quorumSize + 1, _options.Cluster.PrepareAckCount);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class with_commit_ack_count_set_higher_than_the_quorum<TLogFormat, TStreamId> : ClusterMemberScenario<TLogFormat, TStreamId> {
		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) =>
			options with {
				Cluster = options.Cluster with {
					CommitCount = _quorumSize + 1
				}
			};

		[Test]
		public void should_set_commit_count_to_the_given_value() {
			Assert.AreEqual(_quorumSize + 1, _options.Cluster.CommitAckCount);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class with_prepare_ack_count_set_lower_than_the_quorum<TLogFormat, TStreamId> : ClusterMemberScenario<TLogFormat, TStreamId> {
		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) =>
			options with {
				Cluster = options.Cluster with {
					PrepareCount = _quorumSize - 1
				}
			};

		[Test]
		public void should_set_prepare_count_to_the_quorum_size() {
			Assert.AreEqual(_quorumSize, _options.Cluster.PrepareAckCount);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class with_commit_ack_count_set_lower_than_the_quorum<TLogFormat, TStreamId> : ClusterMemberScenario<TLogFormat, TStreamId> {
		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) =>
			options with {
				Cluster = options.Cluster with {
					CommitCount = _quorumSize - 1
				}
			};

		[Test]
		public void should_set_commit_count_to_the_quorum_size() {
			Assert.AreEqual(_quorumSize, _options.Cluster.CommitAckCount);
		}
	}


	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class with_custom_gossip_seeds<TLogFormat, TStreamId> : ClusterMemberScenario<TLogFormat, TStreamId> {
		private readonly DnsEndPoint[] _gossipSeeds = {new("127.0.1.15", 2112), new("127.0.1.15", 3112)};

		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) =>
			options.WithGossipSeeds(_gossipSeeds);

		[Test]
		public void should_turn_off_discovery_by_dns() {
			Assert.IsFalse(_options.Cluster.DiscoverViaDns);
		}

		[Test]
		public void should_set_the_gossip_seeds() {
			CollectionAssert.AreEqual(_gossipSeeds, _options.Cluster.GossipSeed);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class with_custom_external_ip_address_as_advertise_info<TLogFormat, TStreamId> : ClusterMemberScenario<TLogFormat, TStreamId> {
		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) =>
			options
				.Insecure()
				.WithExternalTcpOn(new IPEndPoint(IPAddress.Loopback, 11130))
				.WithInternalTcpOn(new IPEndPoint(IPAddress.Loopback, 11120))
				.AdvertiseExternalHostAs(new DnsEndPoint("196.168.1.1", 11131))
				.AdvertiseHttpHostAs(new DnsEndPoint("196.168.1.1", 21130));

		[Test]
		public void should_set_the_custom_advertise_info_for_external() {
			Assert.AreEqual(new DnsEndPoint("196.168.1.1", 11131),
				_node.GossipAdvertiseInfo.ExternalTcp);
			Assert.AreEqual(new DnsEndPoint("196.168.1.1", 21130),
				_node.GossipAdvertiseInfo.HttpEndPoint);
		}

		[Test]
		public void should_set_the_loopback_address_as_advertise_info_for_internal() {
			Assert.AreEqual(new DnsEndPoint(IPAddress.Loopback.ToString(), 11120), _node.GossipAdvertiseInfo.InternalTcp);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class with_0_0_0_0_as_external_ip_address_and_custom_advertise_info<TLogFormat, TStreamId> : ClusterMemberScenario<TLogFormat, TStreamId> {
		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) =>
			options
				.Insecure()
				.WithInternalTcpOn(new IPEndPoint(IPAddress.Any, 11120))
				.WithExternalTcpOn(new IPEndPoint(IPAddress.Any, 11130))
				.AdvertiseExternalHostAs(new DnsEndPoint("10.0.0.1", 11131));

		[Test]
		public void should_set_the_custom_advertise_info_for_external() {
			Assert.AreEqual(new DnsEndPoint("10.0.0.1", 11131),
				_node.GossipAdvertiseInfo.ExternalTcp);
			Assert.AreEqual(new DnsEndPoint("10.0.0.1", 2113),
				_node.GossipAdvertiseInfo.HttpEndPoint);
		}

		[Test]
		public void should_set_the_non_loopback_address_as_advertise_info_for_internal() {
			Assert.AreEqual(new DnsEndPoint(IPFinder.GetNonLoopbackAddress().ToString(), 11120),
				_node.GossipAdvertiseInfo.InternalTcp);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class with_0_0_0_0_as_external_ip_address_with_no_explicit_advertise_info_set<TLogFormat, TStreamId> : ClusterMemberScenario<TLogFormat, TStreamId> {
		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) =>
			options
				.Insecure()
				.WithHttpOn(new IPEndPoint(IPAddress.Any, 21130))
				.WithExternalTcpOn(new IPEndPoint(IPAddress.Any, 11130))
				.WithInternalTcpOn(new IPEndPoint(IPAddress.Loopback, 11120));

		[Test]
		public void should_use_the_non_default_loopback_ip_as_advertise_info_for_external() {
			Assert.AreEqual(new DnsEndPoint(IPFinder.GetNonLoopbackAddress().ToString(), 11130),
				_node.GossipAdvertiseInfo.ExternalTcp);
			Assert.AreEqual(new DnsEndPoint(IPFinder.GetNonLoopbackAddress().ToString(), 21130),
				_node.GossipAdvertiseInfo.HttpEndPoint);
		}

		[Test]
		public void should_use_loopback_ip_as_advertise_info_for_internal() {
			Assert.AreEqual(new DnsEndPoint(IPAddress.Loopback.ToString(), 11120), _node.GossipAdvertiseInfo.InternalTcp);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class
		with_0_0_0_0_for_internal_and_external_ips_with_advertise_info_set_for_external<TLogFormat, TStreamId> : ClusterMemberScenario<TLogFormat, TStreamId> {
		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) =>
			options
				.Insecure()
				.WithHttpOn(new IPEndPoint(IPAddress.Loopback, 21130))
				.WithExternalTcpOn(new IPEndPoint(IPAddress.Loopback, 11130))
				.WithInternalTcpOn(new IPEndPoint(IPAddress.Any, 11120))
				.AdvertiseExternalHostAs(new DnsEndPoint("10.0.0.1", 11131))
				.AdvertiseHttpHostAs(new DnsEndPoint("10.0.0.1", 21131));

		[Test]
		public void should_set_the_custom_advertise_info_for_external() {
			Assert.AreEqual(new DnsEndPoint("10.0.0.1", 11131),
				_node.GossipAdvertiseInfo.ExternalTcp);
			Assert.AreEqual(new DnsEndPoint("10.0.0.1", 21131),
				_node.GossipAdvertiseInfo.HttpEndPoint);
		}

		[Test]
		public void should_use_the_non_default_loopback_ip_as_advertise_info_for_internal() {
			Assert.AreEqual(new DnsEndPoint(IPFinder.GetNonLoopbackAddress().ToString(), 11120),
				_node.GossipAdvertiseInfo.InternalTcp);
		}
	}
}
