using NUnit.Framework;
using System;
using System.Net;

namespace EventStore.Core.Tests.Common.VNodeBuilderTests.when_building
{
    [TestFixture]
    public class with_cluster_dns_name : ClusterMemberScenario
    {
        public override void Given()
        {
            _builder.WithClusterDnsName("ClusterDns");
        }

        [Test]
        public void should_set_discover_via_dns_to_true()
        {
            Assert.IsTrue(_settings.DiscoverViaDns);
        }

        [Test]
        public void should_set_cluster_dns_name()
        {
            Assert.AreEqual("ClusterDns", _settings.ClusterDns);
        }
    }

    [TestFixture]
    public class with_dns_discovery_disabled_and_no_gossip_seeds
    {
        private Exception _caughtException;
        protected VNodeBuilder _builder;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            _builder = TestVNodeBuilder.AsClusterMember(3)
                                       .RunInMemory()
                                       .OnDefaultEndpoints()
                                       .DisableDnsDiscovery();
            try 
            {
                _builder.Build();
            } 
            catch(Exception e) 
            {
                _caughtException = e;
            }
        }

        [Test]
        public void should_throw_an_exception()
        {
            Assert.IsNotNull(_caughtException);
        }
    }

    [TestFixture]
    public class with_dns_discovery_disabled_and_gossip_seeds_defined : ClusterMemberScenario
    {
        private IPEndPoint[] _gossipSeeds;

        public override void Given()
        {
            var baseAddress = IPAddress.Parse("192.168.1.10");
            _gossipSeeds = new IPEndPoint[] {
                new IPEndPoint(baseAddress, 1111),
                new IPEndPoint(baseAddress, 1112)
            };
            _builder.DisableDnsDiscovery()
                    .WithGossipSeeds(_gossipSeeds);
        }

        [Test]
        public void should_set_discover_via_dns_to_false()
        {
            Assert.IsFalse(_settings.DiscoverViaDns);
        }

        [Test]
        public void should_set_the_gossip_seeds()
        {
            CollectionAssert.AreEqual(_gossipSeeds, _settings.GossipSeeds);
        }
    }

    [TestFixture]
    public class with_prepare_ack_count_set_higher_than_the_quorum : ClusterMemberScenario
    {
        public override void Given()
        {
            _builder.WithPrepareCount(_quorumSize + 1);
        }

        [Test]
        public void should_set_prepare_count_to_the_given_value()
        {
            Assert.AreEqual(_quorumSize + 1, _settings.PrepareAckCount);
        }
    }

    [TestFixture]
    public class with_commit_ack_count_set_higher_than_the_quorum : ClusterMemberScenario
    {
        public override void Given()
        {
            _builder.WithCommitCount(_quorumSize + 1);
        }

        [Test]
        public void should_set_commit_count_to_the_given_value()
        {
            Assert.AreEqual(_quorumSize + 1, _settings.CommitAckCount);
        }
    }

    [TestFixture]
    public class with_prepare_ack_count_set_lower_than_the_quorum : ClusterMemberScenario
    {
        public override void Given()
        {
            _builder.WithPrepareCount(_quorumSize - 1);
        }

        [Test]
        public void should_set_prepare_count_to_the_quorum_size()
        {
            Assert.AreEqual(_quorumSize, _settings.PrepareAckCount);
        }
    }

    [TestFixture]
    public class with_commit_ack_count_set_lower_than_the_quorum : ClusterMemberScenario
    {
        public override void Given()
        {
            _builder.WithCommitCount(_quorumSize - 1);
        }

        [Test]
        public void should_set_commit_count_to_the_quorum_size()
        {
            Assert.AreEqual(_quorumSize, _settings.CommitAckCount);
        }
    }

    [TestFixture]
    public class with_custom_node_priority : ClusterMemberScenario
    {
        public override void Given()
        {
            _builder.WithNodePriority(5);
        }

        [Test]
        public void should_set_the_node_priority()
        {
            Assert.AreEqual(5, _settings.NodePriority);
        }
    }

    [TestFixture]
    public class with_custom_gossip_seeds : ClusterMemberScenario
    {
        private IPEndPoint[] _gossipSeeds;
        public override void Given()
        {
            var baseIpAddress = IPAddress.Parse("192.168.1.15");
            _gossipSeeds = new IPEndPoint[] { new IPEndPoint(baseIpAddress, 2112), new IPEndPoint(baseIpAddress, 3112)};
            _builder.WithGossipSeeds(_gossipSeeds);
        }

        [Test]
        public void should_turn_off_discovery_by_dns()
        {
            Assert.IsFalse(_settings.DiscoverViaDns);
        }

        [Test]
        public void should_set_the_gossip_seeds()
        {
            CollectionAssert.AreEqual(_gossipSeeds, _settings.GossipSeeds);
        }
    }

    [TestFixture]
    public class with_custom_gossip_interval : ClusterMemberScenario
    {
        public override void Given()
        {
            _builder.WithGossipInterval(TimeSpan.FromMilliseconds(1300));
        }

        [Test]
        public void should_set_the_gossip_interval()
        {
            Assert.AreEqual(1300, _settings.GossipInterval.TotalMilliseconds);
        }
    }

    [TestFixture]
    public class with_custom_gossip_allowed_time_difference : ClusterMemberScenario
    {
        public override void Given()
        {
            _builder.WithGossipAllowedTimeDifference(TimeSpan.FromMilliseconds(1300));
        }

        [Test]
        public void should_set_the_allowed_gossip_time_difference()
        {
            Assert.AreEqual(1300, _settings.GossipAllowedTimeDifference.TotalMilliseconds);
        }
    }

    [TestFixture]
    public class with_custom_gossip_timeout : ClusterMemberScenario
    {
        public override void Given()
        {
            _builder.WithGossipTimeout(TimeSpan.FromMilliseconds(1300));
        }

        [Test]
        public void should_set_the_gossip_timeout()
        {
            Assert.AreEqual(1300, _settings.GossipTimeout.TotalMilliseconds);
        }
    }
}
