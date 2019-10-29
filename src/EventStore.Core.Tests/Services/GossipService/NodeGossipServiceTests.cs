using System;
using System.Net;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Gossip;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Infrastructure;
using EventStore.Core.Tests.Services.ElectionsService;
using EventStore.Core.TransactionLog.Checkpoint;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.GossipService {
	public abstract class NodeGossipServiceTestFixture {
		public NodeGossipService SUT;
		protected FakePublisher _publisher;
		protected VNodeInfo _nodeOne;
		protected VNodeInfo _nodeTwo;
		protected VNodeInfo _nodeThree;

		protected NodeGossipServiceTestFixture() {
			_publisher = new FakePublisher();
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
				new InMemoryCheckpoint(0), new InMemoryCheckpoint(0), new FakeEpochManager(), () => 0L, 0,
				TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(1000));
		}
	}

	public class when_system_init : NodeGossipServiceTestFixture {
		[Test]
		public void should_request_gossip_sources() {
			SUT.Handle(new SystemMessage.SystemInit());
			
			var expected = new Message[] {
				new GossipMessage.GotGossipSeedSources(new []{_nodeOne.InternalHttp, _nodeTwo.InternalHttp, _nodeThree.InternalHttp}),
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}
}
