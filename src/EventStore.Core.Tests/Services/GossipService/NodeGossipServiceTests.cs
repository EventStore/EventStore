using System;
using System.Linq;
using System.Net;
using EventStore.Core.Cluster;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Gossip;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Infrastructure;
using EventStore.Core.Tests.Services.ElectionsService;
using EventStore.Core.TransactionLog.Checkpoint;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.GossipService {
	public abstract class NodeGossipServiceTestFixture {
		public Func<DateTime> GetUtcNow;
		public Func<DateTime> GetNow;
		public NodeGossipService SUT;
		protected FakePublisher _publisher;
		protected VNodeInfo _nodeOne;
		protected VNodeInfo _nodeTwo;
		protected VNodeInfo _nodeThree;
		protected Guid _epochId;
		protected TimeSpan _gossipInterval;
		protected TimeSpan _allowedTimeDifference;
		protected Func<long> _getLastCommitPosition;

		protected NodeGossipServiceTestFixture() {
			var utcNow = DateTime.UtcNow;
			GetUtcNow = () => utcNow;

			var now = DateTime.Now;
			GetNow = () => now;

			_epochId = Guid.NewGuid();
			_publisher = new FakePublisher();

			_gossipInterval = TimeSpan.FromMilliseconds(1000);
			_allowedTimeDifference = TimeSpan.FromMilliseconds(1000);
			_getLastCommitPosition = () => 0L;

			_nodeOne = new VNodeInfo(
				Guid.Parse("00000000-0000-0000-0000-000000000001"), 1,
				new IPEndPoint(IPAddress.Loopback, 1111),
				new IPEndPoint(IPAddress.Loopback, 1111),
				new IPEndPoint(IPAddress.Loopback, 1111),
				new IPEndPoint(IPAddress.Loopback, 1111),
				new IPEndPoint(IPAddress.Loopback, 1111),
				new IPEndPoint(IPAddress.Loopback, 1111), false);
			_nodeTwo = new VNodeInfo(
				Guid.Parse("00000000-0000-0000-0000-000000000002"), 2,
				new IPEndPoint(IPAddress.Loopback, 2222),
				new IPEndPoint(IPAddress.Loopback, 2222),
				new IPEndPoint(IPAddress.Loopback, 2222),
				new IPEndPoint(IPAddress.Loopback, 2222),
				new IPEndPoint(IPAddress.Loopback, 2222),
				new IPEndPoint(IPAddress.Loopback, 2222), false);
			_nodeThree = new VNodeInfo(
				Guid.Parse("00000000-0000-0000-0000-000000000003"), 3,
				new IPEndPoint(IPAddress.Loopback, 3333),
				new IPEndPoint(IPAddress.Loopback, 3333),
				new IPEndPoint(IPAddress.Loopback, 3333),
				new IPEndPoint(IPAddress.Loopback, 3333),
				new IPEndPoint(IPAddress.Loopback, 3333),
				new IPEndPoint(IPAddress.Loopback, 3333), false);
			SUT = new NodeGossipService(_publisher,
				new KnownEndpointGossipSeedSource(new[]
					{_nodeOne.InternalHttp, _nodeTwo.InternalHttp, _nodeThree.InternalHttp}), _nodeOne,
				new InMemoryCheckpoint(0), new InMemoryCheckpoint(0), new FakeEpochManager(), _getLastCommitPosition, 0,
				TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(1000), GetUtcNow, GetNow,
				infos => infos.First(x => x.InternalHttpEndPoint.Equals(_nodeTwo.InternalHttp)));
		}
	}

	public class when_system_init : NodeGossipServiceTestFixture {
		[Test]
		public void should_get_gossip_sources() {
			SUT.Handle(new SystemMessage.SystemInit());

			var expected = new Message[] {
				new GossipMessage.GotGossipSeedSources(new[]
					{_nodeOne.InternalHttp, _nodeTwo.InternalHttp, _nodeThree.InternalHttp}),
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}

	public class when_gossip_sources_are_received : NodeGossipServiceTestFixture {
		[Test]
		public void should_start_gossiping() {
			SUT.Handle(new SystemMessage.SystemInit());
			_publisher.Messages.Clear();
			SUT.Handle(new GossipMessage.GotGossipSeedSources(new[]
				{_nodeOne.InternalHttp, _nodeTwo.InternalHttp, _nodeThree.InternalHttp}));

			var expected = new Message[] {
				TimerMessage.Schedule.Create(GossipServiceBase.GossipStartupInterval, new PublishEnvelope(_publisher),
					new GossipMessage.Gossip(1)),
				new HttpMessage.SendOverHttp(_nodeTwo.InternalHttp, new GossipMessage.SendGossip(new ClusterInfo(
						MemberInfo.ForManager(Guid.Empty, GetUtcNow(), true, _nodeTwo.InternalHttp,
							_nodeTwo.InternalHttp),
						MemberInfo.ForManager(Guid.Empty, GetUtcNow(), true, _nodeThree.InternalHttp,
							_nodeThree.InternalHttp),
						MemberInfo.ForVNode(_nodeOne.InstanceId, GetUtcNow(), VNodeState.Initializing, true,
							_nodeOne.InternalTcp, _nodeOne.InternalSecureTcp, _nodeOne.ExternalTcp,
							_nodeOne.ExternalSecureTcp, _nodeOne.InternalHttp, _nodeOne.ExternalHttp,
							_getLastCommitPosition(), 0, 0, -1, -1, Guid.Empty, 0, false)), _nodeOne.InternalHttp),
					GetNow().Add(_gossipInterval)),
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}

	public class when_gossiping_and_gossip_round_larger_than_or_equal_to_20 : NodeGossipServiceTestFixture {
		[Test]
		public void should_use_gossip_interval_for_next_gossip() {
			SUT.Handle(new SystemMessage.SystemInit());
			SUT.Handle(new GossipMessage.GotGossipSeedSources(new[]
				{_nodeOne.InternalHttp, _nodeTwo.InternalHttp, _nodeThree.InternalHttp}));
			_publisher.Messages.Clear();
			SUT.Handle(new GossipMessage.Gossip(20));

			var expected = new Message[] {
				TimerMessage.Schedule.Create(_gossipInterval, new PublishEnvelope(_publisher),
					new GossipMessage.Gossip(21)),
				new HttpMessage.SendOverHttp(_nodeTwo.InternalHttp, new GossipMessage.SendGossip(new ClusterInfo(
						MemberInfo.ForManager(Guid.Empty, GetUtcNow(), true, _nodeTwo.InternalHttp,
							_nodeTwo.InternalHttp),
						MemberInfo.ForManager(Guid.Empty, GetUtcNow(), true, _nodeThree.InternalHttp,
							_nodeThree.InternalHttp),
						MemberInfo.ForVNode(_nodeOne.InstanceId, GetUtcNow(), VNodeState.Initializing, true,
							_nodeOne.InternalTcp, _nodeOne.InternalSecureTcp, _nodeOne.ExternalTcp,
							_nodeOne.ExternalSecureTcp, _nodeOne.InternalHttp, _nodeOne.ExternalHttp,
							_getLastCommitPosition(), 0, 0, -1, -1, Guid.Empty, 0, false)), _nodeOne.InternalHttp),
					GetNow().Add(_gossipInterval)),
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}

	public class when_receiving_out_of_date_cluster_information_from_gossip : NodeGossipServiceTestFixture {
		[Test]
		public void should_publish_updated_gossip_and_reply_with_updated_gossip() {
			var clusterInfo = new ClusterInfo(
				MemberInfo.ForManager(Guid.Empty, GetUtcNow(), true, _nodeTwo.InternalHttp,
					_nodeTwo.InternalHttp),
				MemberInfo.ForManager(Guid.Empty, GetUtcNow(), true, _nodeThree.InternalHttp,
					_nodeThree.InternalHttp),
				MemberInfo.ForVNode(_nodeOne.InstanceId, GetUtcNow(), VNodeState.Initializing, true,
					_nodeOne.InternalTcp, _nodeOne.InternalSecureTcp, _nodeOne.ExternalTcp,
					_nodeOne.ExternalSecureTcp, _nodeOne.InternalHttp, _nodeOne.ExternalHttp,
					_getLastCommitPosition(), 0, 0, -1, -1, Guid.Empty, 0, false));

			ClusterInfo updatedClusterInfo = null;

			SUT.Handle(new SystemMessage.SystemInit());
			SUT.Handle(new GossipMessage.GotGossipSeedSources(new[]
				{_nodeOne.InternalHttp, _nodeTwo.InternalHttp, _nodeThree.InternalHttp}));
			_publisher.Messages.Clear();
			SUT.Handle(new GossipMessage.GossipReceived(new CallbackEnvelope(
					message => updatedClusterInfo = ((GossipMessage.SendGossip)message).ClusterInfo), clusterInfo,
				_nodeTwo.InternalHttp));

			var expected = new Message[] {
				new GossipMessage.GossipUpdated(updatedClusterInfo), 
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}
	
	public class when_receiving_fresh_cluster_information_from_gossip : NodeGossipServiceTestFixture {
		[Test]
		public void should_update_cluster_information_and_reply_with_updated_gossip() {
			var clusterInfo = new ClusterInfo(
				MemberInfo.ForManager(Guid.Empty, GetUtcNow().AddMilliseconds(1), true, _nodeTwo.InternalHttp,
					_nodeTwo.InternalHttp),
				MemberInfo.ForManager(Guid.Empty, GetUtcNow().AddMilliseconds(1), true, _nodeThree.InternalHttp,
					_nodeThree.InternalHttp),
				MemberInfo.ForVNode(_nodeOne.InstanceId, GetUtcNow(), VNodeState.Initializing, true,
					_nodeOne.InternalTcp, _nodeOne.InternalSecureTcp, _nodeOne.ExternalTcp,
					_nodeOne.ExternalSecureTcp, _nodeOne.InternalHttp, _nodeOne.ExternalHttp,
					_getLastCommitPosition(), 0, 0, -1, -1, Guid.Empty, 0, false));

			ClusterInfo updatedClusterInfo = null;

			SUT.Handle(new SystemMessage.SystemInit());
			SUT.Handle(new GossipMessage.GotGossipSeedSources(new[]
				{_nodeOne.InternalHttp, _nodeTwo.InternalHttp, _nodeThree.InternalHttp}));
			_publisher.Messages.Clear();
			SUT.Handle(new GossipMessage.GossipReceived(new CallbackEnvelope(
					message => updatedClusterInfo = ((GossipMessage.SendGossip)message).ClusterInfo), clusterInfo,
				_nodeTwo.InternalHttp));

			var expected = new Message[] {
				new GossipMessage.GossipUpdated(updatedClusterInfo), 
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}
}
