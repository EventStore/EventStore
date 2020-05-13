using NUnit.Framework;
using System;
using System.Net;
using EventStore.Common;
using EventStore.Common.Utils;

namespace EventStore.Core.Tests.Common.VNodeBuilderTests.when_building {
	[TestFixture]
	public class with_cluster_dns_name : ClusterMemberScenario {
		public override void Given() {
			_builder.WithClusterDnsName("ClusterDns");
		}

		[Test]
		public void should_set_discover_via_dns_to_true() {
			Assert.IsTrue(_settings.DiscoverViaDns);
		}

		[Test]
		public void should_set_cluster_dns_name() {
			Assert.AreEqual("ClusterDns", _settings.ClusterDns);
		}
	}

	[TestFixture]
	public class with_dns_discovery_disabled_and_no_gossip_seeds {
		private Exception _caughtException;
		protected VNodeBuilder _builder;

		[OneTimeSetUp]
		public void TestFixtureSetUp() {
			_builder = TestVNodeBuilder.AsClusterMember(3)
				.RunInMemory()
				.OnDefaultEndpoints()
				.DisableDnsDiscovery();
			try {
				_builder.Build();
			} catch (Exception e) {
				_caughtException = e;
			}
		}

		[Test]
		public void should_throw_an_exception() {
			Assert.IsNotNull(_caughtException);
		}
	}

	[TestFixture]
	public class with_dns_discovery_disabled_and_gossip_seeds_defined : ClusterMemberScenario {
		private EndPoint[] _gossipSeeds;

		public override void Given() {
			var baseAddress = "127.0.1.10";
			_gossipSeeds = new [] {
				new EventStoreEndPoint(baseAddress, 1111),
				new EventStoreEndPoint(baseAddress, 1112),
			};
			_builder.DisableDnsDiscovery()
				.WithGossipSeeds(_gossipSeeds);
		}

		[Test]
		public void should_set_discover_via_dns_to_false() {
			Assert.IsFalse(_settings.DiscoverViaDns);
		}

		[Test]
		public void should_set_the_gossip_seeds() {
			CollectionAssert.AreEqual(_gossipSeeds, _settings.GossipSeeds);
		}
	}

	[TestFixture]
	public class with_prepare_ack_count_set_higher_than_the_quorum : ClusterMemberScenario {
		public override void Given() {
			_builder.WithPrepareCount(_quorumSize + 1);
		}

		[Test]
		public void should_set_prepare_count_to_the_given_value() {
			Assert.AreEqual(_quorumSize + 1, _settings.PrepareAckCount);
		}
	}

	[TestFixture]
	public class with_commit_ack_count_set_higher_than_the_quorum : ClusterMemberScenario {
		public override void Given() {
			_builder.WithCommitCount(_quorumSize + 1);
		}

		[Test]
		public void should_set_commit_count_to_the_given_value() {
			Assert.AreEqual(_quorumSize + 1, _settings.CommitAckCount);
		}
	}

	[TestFixture]
	public class with_prepare_ack_count_set_lower_than_the_quorum : ClusterMemberScenario {
		public override void Given() {
			_builder.WithPrepareCount(_quorumSize - 1);
		}

		[Test]
		public void should_set_prepare_count_to_the_quorum_size() {
			Assert.AreEqual(_quorumSize, _settings.PrepareAckCount);
		}
	}

	[TestFixture]
	public class with_commit_ack_count_set_lower_than_the_quorum : ClusterMemberScenario {
		public override void Given() {
			_builder.WithCommitCount(_quorumSize - 1);
		}

		[Test]
		public void should_set_commit_count_to_the_quorum_size() {
			Assert.AreEqual(_quorumSize, _settings.CommitAckCount);
		}
	}

	[TestFixture]
	public class with_custom_node_priority : ClusterMemberScenario {
		public override void Given() {
			_builder.WithNodePriority(5);
		}

		[Test]
		public void should_set_the_node_priority() {
			Assert.AreEqual(5, _settings.NodePriority);
		}
	}

	[TestFixture]
	public class with_custom_gossip_seeds : ClusterMemberScenario {
		private EndPoint[] _gossipSeeds;

		public override void Given() {
			var baseIpAddress = "127.0.1.15";
			_gossipSeeds = new EventStoreEndPoint[] { new EventStoreEndPoint(baseIpAddress, 2112), new EventStoreEndPoint(baseIpAddress, 3112) };
			_builder.WithGossipSeeds(_gossipSeeds);
		}

		[Test]
		public void should_turn_off_discovery_by_dns() {
			Assert.IsFalse(_settings.DiscoverViaDns);
		}

		[Test]
		public void should_set_the_gossip_seeds() {
			CollectionAssert.AreEqual(_gossipSeeds, _settings.GossipSeeds);
		}
	}

	[TestFixture]
	public class with_custom_gossip_interval : ClusterMemberScenario {
		public override void Given() {
			_builder.WithGossipInterval(TimeSpan.FromMilliseconds(1300));
		}

		[Test]
		public void should_set_the_gossip_interval() {
			Assert.AreEqual(1300, _settings.GossipInterval.TotalMilliseconds);
		}
	}

	[TestFixture]
	public class with_custom_gossip_allowed_time_difference : ClusterMemberScenario {
		public override void Given() {
			_builder.WithGossipAllowedTimeDifference(TimeSpan.FromMilliseconds(1300));
		}

		[Test]
		public void should_set_the_allowed_gossip_time_difference() {
			Assert.AreEqual(1300, _settings.GossipAllowedTimeDifference.TotalMilliseconds);
		}
	}

	[TestFixture]
	public class with_custom_gossip_timeout : ClusterMemberScenario {
		public override void Given() {
			_builder.WithGossipTimeout(TimeSpan.FromMilliseconds(1300));
		}

		[Test]
		public void should_set_the_gossip_timeout() {
			Assert.AreEqual(1300, _settings.GossipTimeout.TotalMilliseconds);
		}
	}

	[TestFixture]
	public class with_custom_external_ip_address_as_advertise_info : ClusterMemberScenario {
		public override void Given() {
			_builder.WithExternalTcpOn(new IPEndPoint(IPAddress.Loopback, 1113))
				.WithInternalTcpOn(new IPEndPoint(IPAddress.Loopback, 1112))
				.AdvertiseExternalHostAs("196.168.1.1");
		}

		[Test]
		public void should_set_the_custom_advertise_info_for_external() {
			Assert.AreEqual(new EventStoreEndPoint("196.168.1.1", 1113),
				_settings.GossipAdvertiseInfo.ExternalTcp);
			Assert.AreEqual(new EventStoreEndPoint("196.168.1.1", 2113),
				_settings.GossipAdvertiseInfo.ExternalHttp);
		}

		[Test]
		public void should_set_the_loopback_address_as_advertise_info_for_internal() {
			Assert.AreEqual(new EventStoreEndPoint(IPAddress.Loopback.ToString(), 1112), _settings.GossipAdvertiseInfo.InternalTcp);
			Assert.AreEqual(new EventStoreEndPoint(IPAddress.Loopback.ToString(), 2112), _settings.GossipAdvertiseInfo.InternalHttp);
		}
	}

	[TestFixture]
	public class with_0_0_0_0_as_external_ip_address_and_custom_advertise_info : ClusterMemberScenario {
		public override void Given() {
			_builder.WithExternalTcpOn(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 1113))
				.WithInternalTcpOn(new IPEndPoint(IPAddress.Loopback, 1112))
				.AdvertiseExternalHostAs("10.0.0.1");
		}

		[Test]
		public void should_set_the_custom_advertise_info_for_external() {
			Assert.AreEqual(new EventStoreEndPoint("10.0.0.1", 1113),
				_settings.GossipAdvertiseInfo.ExternalTcp);
			Assert.AreEqual(new EventStoreEndPoint("10.0.0.1", 2113),
				_settings.GossipAdvertiseInfo.ExternalHttp);
		}

		[Test]
		public void should_set_the_loopback_address_as_advertise_info_for_internal() {
			Assert.AreEqual(new EventStoreEndPoint(IPAddress.Loopback.ToString(), 1112), _settings.GossipAdvertiseInfo.InternalTcp);
			Assert.AreEqual(new EventStoreEndPoint(IPAddress.Loopback.ToString(), 2112), _settings.GossipAdvertiseInfo.InternalHttp);
		}
	}

	[TestFixture]
	public class with_0_0_0_0_as_external_ip_address_with_no_explicit_advertise_info_set : ClusterMemberScenario {
		public override void Given() {
			_builder
				.WithInternalHttpOn(new IPEndPoint(IPAddress.Loopback, 2112))
				.WithExternalHttpOn(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 2113))
				.WithInternalTcpOn(new IPEndPoint(IPAddress.Loopback, 1112))
				.WithExternalTcpOn(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 1113));
		}

		[Test]
		public void should_use_the_non_default_loopback_ip_as_advertise_info_for_external() {
			Assert.AreEqual(new EventStoreEndPoint(IPFinder.GetNonLoopbackAddress().ToString(), 1113),
				_settings.GossipAdvertiseInfo.ExternalTcp);
			Assert.AreEqual(new EventStoreEndPoint(IPFinder.GetNonLoopbackAddress().ToString(), 2113),
				_settings.GossipAdvertiseInfo.ExternalHttp);
		}

		[Test]
		public void should_use_loopback_ip_as_advertise_info_for_internal() {
			Assert.AreEqual(new EventStoreEndPoint(IPAddress.Loopback.ToString(), 1112), _settings.GossipAdvertiseInfo.InternalTcp);
			Assert.AreEqual(new EventStoreEndPoint(IPAddress.Loopback.ToString(), 2112), _settings.GossipAdvertiseInfo.InternalHttp);
		}
	}

	[TestFixture]
	public class
		with_0_0_0_0_for_internal_and_external_ips_with_advertise_info_set_for_external : ClusterMemberScenario {
		public override void Given() {
			_builder
				.WithInternalHttpOn(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 2112))
				.WithExternalHttpOn(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 2113))
				.WithInternalTcpOn(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 1112))
				.WithExternalTcpOn(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 1113))
				.AdvertiseExternalHostAs("10.0.0.1");
		}

		[Test]
		public void should_set_the_custom_advertise_info_for_external() {
			Assert.AreEqual(new EventStoreEndPoint("10.0.0.1", 1113),
				_settings.GossipAdvertiseInfo.ExternalTcp);
			Assert.AreEqual(new EventStoreEndPoint("10.0.0.1", 2113),
				_settings.GossipAdvertiseInfo.ExternalHttp);
		}

		[Test]
		public void should_use_the_non_default_loopback_ip_as_advertise_info_for_internal() {
			Assert.AreEqual(new EventStoreEndPoint(IPFinder.GetNonLoopbackAddress().ToString(), 1112),
				_settings.GossipAdvertiseInfo.InternalTcp);
			Assert.AreEqual(new EventStoreEndPoint(IPFinder.GetNonLoopbackAddress().ToString(), 2112),
				_settings.GossipAdvertiseInfo.InternalHttp);
		}
	}
}
