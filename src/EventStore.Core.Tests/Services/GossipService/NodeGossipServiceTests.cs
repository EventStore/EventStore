﻿using System;
using System.Linq;
using System.Net;
using EventStore.Core.Cluster;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Gossip;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Services.ElectionsService;
using EventStore.Core.Tests.Services.TimeService;
using EventStore.Core.TransactionLog.Checkpoint;
using NUnit.Framework;
using MemberInfo = EventStore.Core.Cluster.MemberInfo;

namespace EventStore.Core.Tests.Services.GossipService {
	public abstract class NodeGossipServiceTestFixture {
		protected NodeGossipService SUT;
		protected FakePublisher _bus;
		protected VNodeInfo _currentNode;
		protected VNodeInfo _nodeTwo;
		protected VNodeInfo _nodeThree;
		protected ITimeProvider _timeProvider;
		protected Func<MemberInfo[], MemberInfo> _getNodeToGossipTo;
		protected IGossipSeedSource _gossipSeedSource;
		protected TimeSpan _gossipInterval = TimeSpan.FromMilliseconds(1000);
		private readonly TimeSpan _allowedTimeDifference = TimeSpan.FromMilliseconds(1000);

		public NodeGossipServiceTestFixture() {
			_bus = new FakePublisher();
			_timeProvider = new FakeTimeProvider();

			_currentNode = new VNodeInfo(
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

			_getNodeToGossipTo = infos => infos.First(x => Equals(x.InternalHttpEndPoint, _nodeTwo.InternalHttp));
			_gossipSeedSource = new KnownEndpointGossipSeedSource(new[]
				{_currentNode.InternalHttp, _nodeTwo.InternalHttp, _nodeThree.InternalHttp});
		}

		[SetUp]
		public void Setup() {
			SUT = new NodeGossipService(_bus, _gossipSeedSource, _currentNode,
				new InMemoryCheckpoint(0), new InMemoryCheckpoint(0), new FakeEpochManager(), () => 0L, 0,
				_gossipInterval, _allowedTimeDifference, _timeProvider, _getNodeToGossipTo);

			foreach (var message in Given()) {
				SUT.Handle((dynamic)message);
			}

			_bus.Messages.Clear();

			var when = When();
			if (when != null) {
				SUT.Handle((dynamic)when);
			}
		}

		protected virtual Message[] Given() => Array.Empty<Message>();

		protected virtual Message When() => null;

		protected void ExpectMessages(params Message[] expected) =>
			AssertEx.AssertUsingDeepCompare(_bus.Messages.ToArray(), expected);

		protected void ExpectNoMessages() =>
			Assert.IsEmpty(_bus.Messages);

		protected Message[] GivenSystemInitializedWithKnownGossipSeedSources(params Message[] additionalGivens) {
			return new Message[] {
				new SystemMessage.SystemInit(),
				new GossipMessage.GotGossipSeedSources(new[]
					{_currentNode.InternalHttp, _nodeTwo.InternalHttp, _nodeThree.InternalHttp})
			}.Concat(additionalGivens).ToArray();
		}

		protected static MemberInfo MemberInfoForVNode(VNodeInfo nodeInfo, DateTime utcNow,
			int? nodePriority = null, int? epochNumber = null, long? writerCheckpoint = null,
			VNodeState nodeState = VNodeState.Initializing) {
			return MemberInfo.ForVNode(nodeInfo.InstanceId, utcNow, nodeState, true,
				nodeInfo.InternalTcp, nodeInfo.InternalSecureTcp, nodeInfo.ExternalTcp,
				nodeInfo.ExternalSecureTcp, nodeInfo.InternalHttp, nodeInfo.ExternalHttp,
				0, writerCheckpoint ?? 0, 0, -1, epochNumber ?? -1, Guid.Empty, nodePriority ?? 0, false);
		}

		/// <summary>
		/// The initial state for a node currently is represented as a Manager
		/// </summary>
		protected static MemberInfo InitialStateForVNode(VNodeInfo nodeInfo, DateTime utcNow, bool isAlive = true) {
			return MemberInfo.ForManager(Guid.Empty, utcNow, isAlive, nodeInfo.InternalHttp,
				nodeInfo.InternalHttp);
		}
	}

	public class when_system_initializes : NodeGossipServiceTestFixture {
		protected override Message[] Given() =>
			Array.Empty<Message>();

		protected override Message When() =>
			new SystemMessage.SystemInit();

		[Test]
		public void should_get_gossip_sources() {
			ExpectMessages(
				new GossipMessage.GotGossipSeedSources(new[]
					{_currentNode.InternalHttp, _nodeTwo.InternalHttp, _nodeThree.InternalHttp}));
		}
	}

	public class when_system_initializes_twice : NodeGossipServiceTestFixture {
		protected override Message[] Given() => new Message[] {
			new SystemMessage.SystemInit()
		};

		protected override Message When() =>
			new SystemMessage.SystemInit();

		[Test]
		public void should_ignore_system_init() {
			ExpectNoMessages();
		}
	}

	public class when_retrieving_gossip_seed_sources : NodeGossipServiceTestFixture {
		protected override Message[] Given() => new Message[] {
			new SystemMessage.SystemInit()
		};

		protected override Message When() =>
			new GossipMessage.RetrieveGossipSeedSources();

		[Test]
		public void should_get_gossip_seeds() {
			ExpectMessages(
				new GossipMessage.GotGossipSeedSources(new[]
					{_currentNode.InternalHttp, _nodeTwo.InternalHttp, _nodeThree.InternalHttp}));
		}
	}

	public class when_retrieving_gossip_seed_sources_and_gossip_seed_source_throws : NodeGossipServiceTestFixture {
		class ThrowingGossipSeedSource : IGossipSeedSource {
			public IAsyncResult BeginGetHostEndpoints(AsyncCallback requestCallback, object state) {
				throw new NotImplementedException();
			}

			public IPEndPoint[] EndGetHostEndpoints(IAsyncResult asyncResult) {
				throw new NotImplementedException();
			}
		}

		public when_retrieving_gossip_seed_sources_and_gossip_seed_source_throws() {
			_gossipSeedSource = new ThrowingGossipSeedSource();
		}

		protected override Message[] Given() => new Message[] {
			new SystemMessage.SystemInit()
		};

		protected override Message When() =>
			new GossipMessage.RetrieveGossipSeedSources();

		[Test]
		public void should_schedule_retry_retrieve_gossip_seed_sources() {
			ExpectMessages(
				TimerMessage.Schedule.Create(GossipServiceBase.DnsRetryTimeout, new PublishEnvelope(_bus),
					new GossipMessage.RetrieveGossipSeedSources()));
		}
	}

	public class when_got_gossip_seed_sources : NodeGossipServiceTestFixture {
		protected override Message[] Given() => new Message[] {
			new SystemMessage.SystemInit()
		};

		protected override Message When() =>
			new GossipMessage.GotGossipSeedSources(new[]
				{_currentNode.InternalHttp, _nodeTwo.InternalHttp, _nodeThree.InternalHttp});

		[Test]
		public void should_start_gossiping_and_schedule_another_gossip() {
			ExpectMessages(
				new HttpMessage.SendOverHttp(_nodeTwo.InternalHttp, new GossipMessage.SendGossip(new ClusterInfo(
						MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
						InitialStateForVNode(_nodeTwo, _timeProvider.UtcNow),
						InitialStateForVNode(_nodeThree, _timeProvider.UtcNow)),
					_currentNode.InternalHttp), _timeProvider.LocalTime.Add(_gossipInterval)),
				TimerMessage.Schedule.Create(GossipServiceBase.GossipStartupInterval, new PublishEnvelope(_bus),
					new GossipMessage.Gossip(1)));
		}
	}

	public class when_gossip : NodeGossipServiceTestFixture {
		private int _gossipRound = GossipServiceBase.GossipRoundStartupThreshold + 1;

		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources();

		protected override Message When() =>
			new GossipMessage.Gossip(_gossipRound);

		[Test]
		public void should_send_the_gossip_over_http_and_schedule_the_next_gossip() {
			ExpectMessages(
				new HttpMessage.SendOverHttp(_nodeTwo.InternalHttp, new GossipMessage.SendGossip(new ClusterInfo(
						MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
						InitialStateForVNode(_nodeTwo, _timeProvider.UtcNow),
						InitialStateForVNode(_nodeThree, _timeProvider.UtcNow)),
					_currentNode.InternalHttp), _timeProvider.LocalTime.Add(_gossipInterval)),
				TimerMessage.Schedule.Create(_gossipInterval, new PublishEnvelope(_bus),
					new GossipMessage.Gossip(++_gossipRound)));
		}
	}

	public class when_gossip_and_no_node_is_selected_to_gossip_to : NodeGossipServiceTestFixture {
		private int _gossipRound = GossipServiceBase.GossipRoundStartupThreshold + 1;

		public when_gossip_and_no_node_is_selected_to_gossip_to() {
			_getNodeToGossipTo = infos => null;
		}

		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources();

		protected override Message When() =>
			new GossipMessage.Gossip(_gossipRound);

		[Test]
		public void should_just_schedule_next_gossip() {
			ExpectMessages(TimerMessage.Schedule.Create(_gossipInterval, new PublishEnvelope(_bus),
				new GossipMessage.Gossip(_gossipRound)));
		}
	}

	public class when_gossip_and_gossip_service_is_not_in_working_state : NodeGossipServiceTestFixture {
		protected override Message[] Given() => Array.Empty<Message>();

		protected override Message When() =>
			new GossipMessage.Gossip(1);

		[Test]
		public void should_ignore_message() {
			ExpectNoMessages();
		}
	}

	public class when_gossip_and_gossip_round_less_than_startup_gossip_threshold : NodeGossipServiceTestFixture {
		private int _gossipRound = new Random().Next(0, GossipServiceBase.GossipRoundStartupThreshold);

		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources();

		protected override Message When() =>
			new GossipMessage.Gossip(_gossipRound);

		[Test]
		public void should_use_startup_gossip_interval() {
			ExpectMessages(
				new HttpMessage.SendOverHttp(_nodeTwo.InternalHttp, new GossipMessage.SendGossip(new ClusterInfo(
						MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
						InitialStateForVNode(_nodeTwo, _timeProvider.UtcNow),
						InitialStateForVNode(_nodeThree, _timeProvider.UtcNow)),
					_currentNode.InternalHttp), _timeProvider.LocalTime.Add(_gossipInterval)),
				TimerMessage.Schedule.Create(GossipServiceBase.GossipStartupInterval, new PublishEnvelope(_bus),
					new GossipMessage.Gossip(++_gossipRound)));
		}
	}

	public class when_gossip_and_gossip_round_larger_than_startup_gossip_threshold : NodeGossipServiceTestFixture {
		private int _gossipRound = new Random().Next(GossipServiceBase.GossipRoundStartupThreshold, int.MaxValue);

		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources();

		protected override Message When() =>
			new GossipMessage.Gossip(_gossipRound);

		[Test]
		public void should_use_provided_gossip_interval_for_next_gossip() {
			ExpectMessages(
				new HttpMessage.SendOverHttp(_nodeTwo.InternalHttp, new GossipMessage.SendGossip(new ClusterInfo(
						MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
						InitialStateForVNode(_nodeTwo, _timeProvider.UtcNow),
						InitialStateForVNode(_nodeThree, _timeProvider.UtcNow)),
					_currentNode.InternalHttp), _timeProvider.LocalTime.Add(_gossipInterval)),
				TimerMessage.Schedule.Create(_gossipInterval, new PublishEnvelope(_bus),
					new GossipMessage.Gossip(++_gossipRound)));
		}
	}

	public class when_gossip_received_with_older_timestamp_about_peer_node :
		NodeGossipServiceTestFixture {
		private readonly DateTime _timestamp = DateTime.Now;

		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources(
				new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
						MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
						MemberInfoForVNode(_nodeTwo, _timestamp),
						MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow)),
					_nodeTwo.InternalHttp)
			);

		protected override Message When() =>
			new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeTwo, _timestamp.AddMilliseconds(-1)),
					MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow)),
				_nodeTwo.InternalHttp);

		[Test]
		public void should_accept_the_information_about_the_node_even_if_outdated() {
			ExpectMessages(
				new GossipMessage.GossipUpdated(new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeTwo, _timestamp.AddMilliseconds(-1)),
					MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow))));
		}
	}

	public class when_gossip_received_with_lower_epoch_number_about_peer_node :
		NodeGossipServiceTestFixture {
		private readonly int _epochNumber = new Random().Next();

		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources(
				new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
						MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
						MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow, epochNumber: _epochNumber),
						MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow)),
					_nodeTwo.InternalHttp)
			);

		protected override Message When() =>
			new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow, epochNumber: _epochNumber - 1),
					MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow)),
				_nodeTwo.InternalHttp);

		[Test]
		public void should_accept_the_information_about_the_node_even_if_outdated() {
			ExpectMessages(
				new GossipMessage.GossipUpdated(new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow, epochNumber: _epochNumber - 1),
					MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow))));
		}
	}

	public class when_gossip_received_with_lower_writer_checkpoint_about_peer_node :
		NodeGossipServiceTestFixture {
		private readonly int _writerCheckpoint = new Random().Next();

		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources(
				new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
						MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
						MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow, writerCheckpoint: _writerCheckpoint),
						MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow)),
					_nodeTwo.InternalHttp)
			);

		protected override Message When() =>
			new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow, writerCheckpoint: _writerCheckpoint - 1),
					MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow)),
				_nodeTwo.InternalHttp);

		[Test]
		public void should_accept_the_information_about_the_node_even_if_outdated() {
			ExpectMessages(
				new GossipMessage.GossipUpdated(new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow, writerCheckpoint: _writerCheckpoint - 1),
					MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow))));
		}
	}

	public class when_gossip_received_with_more_recent_timestamp_about_non_peer_node :
		NodeGossipServiceTestFixture {
		private readonly DateTime _timestamp = DateTime.Now;

		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources(
				new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
						MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
						MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow),
						MemberInfoForVNode(_nodeThree, _timestamp)),
					_nodeTwo.InternalHttp)
			);

		protected override Message When() =>
			new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeThree, _timestamp.AddMilliseconds(1))),
				_nodeThree.InternalHttp);

		[Test]
		public void should_accept_the_information_if_its_more_recent() {
			ExpectMessages(
				new GossipMessage.GossipUpdated(new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeThree, _timestamp.AddMilliseconds(1)))));
		}
	}

	public class when_gossip_received_with_higher_epoch_number_about_non_peer_node :
		NodeGossipServiceTestFixture {
		private readonly int _epochNumber = new Random().Next();

		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources(
				new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
						MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
						MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow),
						MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow, epochNumber: _epochNumber)),
					_nodeTwo.InternalHttp)
			);

		protected override Message When() =>
			new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow, epochNumber: _epochNumber + 1)),
				_nodeTwo.InternalHttp);

		[Test]
		public void should_accept_the_information_if_its_more_recent() {
			ExpectMessages(
				new GossipMessage.GossipUpdated(new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow, epochNumber: _epochNumber + 1))));
		}
	}

	public class when_gossip_received_with_higher_writer_checkpoint_about_non_peer_node :
		NodeGossipServiceTestFixture {
		private readonly int _writerCheckpoint = new Random().Next();

		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources(
				new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
						MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
						MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow),
						MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow, writerCheckpoint: _writerCheckpoint)),
					_nodeTwo.InternalHttp)
			);

		protected override Message When() =>
			new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow, writerCheckpoint: _writerCheckpoint + 1)),
				_nodeTwo.InternalHttp);

		[Test]
		public void should_accept_the_information_if_its_more_recent() {
			ExpectMessages(
				new GossipMessage.GossipUpdated(new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow, writerCheckpoint: _writerCheckpoint + 1))));
		}
	}

	public class when_state_changed_to_master : NodeGossipServiceTestFixture {
		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources(
				new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
						MemberInfoForVNode(_currentNode, _timeProvider.UtcNow, nodeState: VNodeState.Initializing),
						InitialStateForVNode(_nodeTwo, _timeProvider.UtcNow),
						InitialStateForVNode(_nodeThree, _timeProvider.UtcNow)),
					_nodeTwo.InternalHttp)
			);

		protected override Message When() =>
			new SystemMessage.BecomeMaster(Guid.NewGuid());

		[Test]
		public void should_update_gossip() {
			ExpectMessages(
				new GossipMessage.GossipUpdated(new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow, nodeState: VNodeState.Master),
					InitialStateForVNode(_nodeTwo, _timeProvider.UtcNow),
					InitialStateForVNode(_nodeThree, _timeProvider.UtcNow))));
		}
	}

	public class when_state_changed_to_non_master : NodeGossipServiceTestFixture {
		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources(
				new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow, nodeState: VNodeState.Initializing),
					InitialStateForVNode(_nodeTwo, _timeProvider.UtcNow),
					InitialStateForVNode(_nodeThree, _timeProvider.UtcNow)), _nodeTwo.InternalHttp)
			);

		protected override Message When() =>
			new SystemMessage.BecomeSlave(Guid.NewGuid(), _nodeTwo);

		[Test]
		public void should_update_gossip() {
			ExpectMessages(
				new GossipMessage.GossipUpdated(new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow, nodeState: VNodeState.Slave),
					InitialStateForVNode(_nodeTwo, _timeProvider.UtcNow),
					InitialStateForVNode(_nodeThree, _timeProvider.UtcNow))));
		}
	}

	public class when_gossip_send_failed : NodeGossipServiceTestFixture {
		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources();

		protected override Message When() =>
			new GossipMessage.GossipSendFailed("failed", _nodeTwo.InternalHttp);

		[Test]
		public void should_mark_the_node_as_dead() {
			ExpectMessages(
				new GossipMessage.GossipUpdated(new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					InitialStateForVNode(_nodeTwo, _timeProvider.UtcNow, isAlive: false),
					InitialStateForVNode(_nodeThree, _timeProvider.UtcNow))));
		}
	}

	public class when_gossip_send_failed_to_a_dead_node : NodeGossipServiceTestFixture {
		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources(
				new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					InitialStateForVNode(_nodeTwo, _timeProvider.UtcNow, isAlive: false),
					InitialStateForVNode(_nodeThree, _timeProvider.UtcNow)), _nodeTwo.InternalHttp)
			);

		protected override Message When() => new GossipMessage.GossipSendFailed("failed",
			_nodeTwo.InternalHttp);

		[Test]
		public void should_ignore_message() {
			ExpectNoMessages();
		}
	}

	public class when_gossip_send_failed_to_the_current_master_node : NodeGossipServiceTestFixture {
		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources(
				new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
						MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
						MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow),
						MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow, nodeState: VNodeState.Master)),
					_nodeTwo.InternalHttp),
				new SystemMessage.BecomeSlave(Guid.NewGuid(), _nodeTwo)
			);

		protected override Message When() => new GossipMessage.GossipSendFailed("failed",
			_nodeTwo.InternalHttp);

		[Test]
		public void should_ignore_message_and_wait_for_tcp_to_decide() {
			ExpectNoMessages();
		}
	}

	public class when_vnode_connection_lost : NodeGossipServiceTestFixture {
		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources(
				new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					InitialStateForVNode(_nodeTwo, _timeProvider.UtcNow),
					InitialStateForVNode(_nodeThree, _timeProvider.UtcNow)), _nodeTwo.InternalHttp)
			);

		protected override Message When() =>
			new SystemMessage.VNodeConnectionLost(_currentNode.InternalHttp, Guid.NewGuid());

		[Test]
		public void should_issue_get_gossip() {
			ExpectMessages(
				new HttpMessage.SendOverHttp(_currentNode.InternalHttp, new GossipMessage.GetGossip(),
					_timeProvider.LocalTime.Add(GossipServiceBase.GossipTimeout)));
		}
	}

	public class when_vnode_connection_lost_to_dead_node : NodeGossipServiceTestFixture {
		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources(
				new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					InitialStateForVNode(_nodeTwo, _timeProvider.UtcNow, isAlive: false),
					InitialStateForVNode(_nodeThree, _timeProvider.UtcNow)), _nodeTwo.InternalHttp)
			);

		protected override Message When() =>
			new SystemMessage.VNodeConnectionLost(_nodeTwo.InternalHttp, Guid.NewGuid());

		[Test]
		public void should_ignore_message() {
			ExpectNoMessages();
		}
	}

	public class when_get_gossip_received_with_more_recent_timestamp_about_non_peer_node :
		NodeGossipServiceTestFixture {
		private readonly DateTime _timestamp = DateTime.Now;

		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources(
				new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
						MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
						MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow),
						MemberInfoForVNode(_nodeThree, _timestamp)),
					_nodeTwo.InternalHttp)
			);

		protected override Message When() =>
			new GossipMessage.GetGossipReceived(new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeThree, _timestamp.AddMilliseconds(1))),
				_nodeThree.InternalHttp);

		[Test]
		public void should_accept_the_information_if_its_more_recent() {
			ExpectMessages(
				new GossipMessage.GossipUpdated(new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeThree, _timestamp.AddMilliseconds(1)))));
		}
	}

	public class when_get_gossip_received_with_higher_epoch_number_about_non_peer_node :
		NodeGossipServiceTestFixture {
		private readonly int _epochNumber = new Random().Next();

		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources(
				new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
						MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
						MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow),
						MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow, epochNumber: _epochNumber)),
					_nodeTwo.InternalHttp)
			);

		protected override Message When() =>
			new GossipMessage.GetGossipReceived(new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow, epochNumber: _epochNumber + 1)),
				_nodeTwo.InternalHttp);

		[Test]
		public void should_accept_the_information_if_its_more_recent() {
			ExpectMessages(
				new GossipMessage.GossipUpdated(new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow, epochNumber: _epochNumber + 1))));
		}
	}

	public class when_get_gossip_received_with_higher_writer_checkpoint_about_non_peer_node :
		NodeGossipServiceTestFixture {
		private readonly int _writerCheckpoint = new Random().Next();

		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources(
				new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
						MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
						MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow),
						MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow,
							writerCheckpoint: _writerCheckpoint)),
					_nodeTwo.InternalHttp)
			);

		protected override Message When() =>
			new GossipMessage.GetGossipReceived(new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow,
						writerCheckpoint: _writerCheckpoint + 1)),
				_nodeTwo.InternalHttp);

		[Test]
		public void should_accept_the_information_if_its_more_recent() {
			ExpectMessages(
				new GossipMessage.GossipUpdated(new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow),
					MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow,
						writerCheckpoint: _writerCheckpoint + 1))));
		}
	}

	public class
		when_get_gossip_received_and_gossip_service_is_not_in_working_state : NodeGossipServiceTestFixture {
		protected override Message[] Given() => Array.Empty<Message>();

		protected override Message When() =>
			new GossipMessage.GetGossipReceived(new ClusterInfo(), _nodeTwo.InternalHttp);

		[Test]
		public void should_ignore_message() {
			ExpectNoMessages();
		}
	}

	public class
		when_get_gossip_failed_and_gossip_service_is_not_in_working_state : NodeGossipServiceTestFixture {
		protected override Message[] Given() => Array.Empty<Message>();

		protected override Message When() =>
			new GossipMessage.GetGossipFailed("failed", _nodeTwo.InternalHttp);

		[Test]
		public void should_ignore_message() {
			ExpectNoMessages();
		}
	}

	public class when_get_gossip_failed : NodeGossipServiceTestFixture {
		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources(
				new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
						MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
						InitialStateForVNode(_nodeTwo, _timeProvider.UtcNow, isAlive: true),
						InitialStateForVNode(_nodeThree, _timeProvider.UtcNow)),
					_currentNode.InternalHttp)
			);

		protected override Message When() =>
			new GossipMessage.GetGossipFailed("failed", _nodeTwo.InternalHttp);

		[Test]
		public void should_mark_node_as_dead() {
			ExpectMessages(
				new GossipMessage.GossipUpdated(new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					InitialStateForVNode(_nodeTwo, _timeProvider.UtcNow, isAlive: false),
					InitialStateForVNode(_nodeThree, _timeProvider.UtcNow))));
		}
	}

	public class when_vnode_connection_established : NodeGossipServiceTestFixture {
		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources(
				new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
						MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
						InitialStateForVNode(_nodeTwo, _timeProvider.UtcNow, isAlive: false),
						InitialStateForVNode(_nodeThree, _timeProvider.UtcNow)),
					_currentNode.InternalHttp)
			);

		protected override Message When() =>
			new SystemMessage.VNodeConnectionEstablished(_nodeTwo.InternalHttp, Guid.NewGuid());

		[Test]
		public void should_mark_node_as_alive() {
			ExpectMessages(
				new GossipMessage.GossipUpdated(new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
					InitialStateForVNode(_nodeTwo, _timeProvider.UtcNow, isAlive: true),
					InitialStateForVNode(_nodeThree, _timeProvider.UtcNow))
				));
		}
	}

	public class when_elections_are_done : NodeGossipServiceTestFixture {
		protected override Message[] Given() =>
			GivenSystemInitializedWithKnownGossipSeedSources(
				new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(
						MemberInfoForVNode(_currentNode, _timeProvider.UtcNow,
							nodeState: VNodeState.Initializing),
						MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow, nodeState: VNodeState.Initializing),
						MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow,
							nodeState: VNodeState.Initializing)),
					_nodeTwo.InternalHttp)
			);

		protected override Message When() =>
			new ElectionMessage.ElectionsDone(0,
				MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow, nodeState: VNodeState.Master));

		[Test]
		public void should_set_master_node_and_other_nodes_to_unknown() {
			ExpectMessages(
				new GossipMessage.GossipUpdated(new ClusterInfo(
					MemberInfoForVNode(_currentNode, _timeProvider.UtcNow, nodeState: VNodeState.Unknown),
					MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow, nodeState: VNodeState.Master),
					MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow, nodeState: VNodeState.Unknown))));
		}
	}

	public class when_updating_node_priority : NodeGossipServiceTestFixture {
		private readonly int _nodePriority = new Random().Next();
		protected override Message[] Given() => GivenSystemInitializedWithKnownGossipSeedSources();

		protected override Message When() {
			return new GossipMessage.UpdateNodePriority(_nodePriority);
		}

		[Test]
		public void should_set_node_priority() {
			var clusterInfo = new ClusterInfo(
				MemberInfoForVNode(_currentNode, _timeProvider.UtcNow),
				MemberInfoForVNode(_nodeTwo, _timeProvider.UtcNow),
				MemberInfoForVNode(_nodeThree, _timeProvider.UtcNow));

			SUT.Handle(new GossipMessage.GossipReceived(new NoopEnvelope(), clusterInfo,
				_nodeTwo.InternalHttp));
			var memberInfo = _bus.Messages.OfType<GossipMessage.GossipUpdated>().First().ClusterInfo.Members
				.First(x => x.InstanceId == _currentNode.InstanceId);
			Assert.AreEqual(memberInfo.NodePriority, _nodePriority);
		}
	}

	public class when_updating_cluster {
		private static MemberInfo TestNodeFor(int identifier, bool isAlive, DateTime timeStamp) {
			var ipEndpoint = new IPEndPoint(IPAddress.Loopback, identifier);
			return MemberInfo.ForVNode(Guid.NewGuid(), timeStamp, VNodeState.Initializing, isAlive,
				ipEndpoint, ipEndpoint, ipEndpoint, ipEndpoint, ipEndpoint, ipEndpoint,
				0, 0, 0, -1, -1, Guid.Empty, 0, false);
		}

		[Test]
		public void
			should_remove_dead_members_which_have_timestamps_older_than_the_allowed_dead_member_removal_timeout() {
			var timeProvider = new FakeTimeProvider();
			var deadMemberRemovalTimeout = TimeSpan.FromSeconds(1);

			var nodeToBeRemoved =
				TestNodeFor(2, isAlive: false, timeProvider.UtcNow.Subtract(deadMemberRemovalTimeout));

			var updatedCluster = GossipServiceBase.UpdateCluster(new ClusterInfo(
					TestNodeFor(1, isAlive: false, timeProvider.UtcNow),
					nodeToBeRemoved,
					TestNodeFor(3, isAlive: false, timeProvider.UtcNow)), info => info,
				timeProvider, deadMemberRemovalTimeout);

			Assert.That(updatedCluster.Members, Has.Length.EqualTo(2));
			Assert.IsFalse(updatedCluster.Members.Any(x => x.Is(nodeToBeRemoved.InternalHttpEndPoint)));
		}

		[Test]
		public void
			should_not_remove_alive_members_which_have_timestamps_older_than_the_allowed_dead_member_removal_timeout() {
			var timeProvider = new FakeTimeProvider();
			var deadMemberRemovalTimeout = TimeSpan.FromSeconds(1);

			var nodeToNotBeRemoved =
				TestNodeFor(2, isAlive: true, timeProvider.UtcNow.Subtract(deadMemberRemovalTimeout));

			var updatedCluster = GossipServiceBase.UpdateCluster(new ClusterInfo(
					TestNodeFor(1, isAlive: false, timeProvider.UtcNow),
					nodeToNotBeRemoved,
					TestNodeFor(3, isAlive: false, timeProvider.UtcNow)), info => info,
				timeProvider, deadMemberRemovalTimeout);

			Assert.That(updatedCluster.Members, Has.Length.EqualTo(3));
			Assert.IsTrue(updatedCluster.Members.Any(x => x.Is(nodeToNotBeRemoved.InternalHttpEndPoint)));
		}
	}

	public class when_merging_clusters {
		private static MemberInfo TestNodeFor(int identifier, bool isAlive, DateTime timeStamp) {
			var ipEndpoint = new IPEndPoint(IPAddress.Loopback, identifier);
			return MemberInfo.ForVNode(Guid.NewGuid(), timeStamp, VNodeState.Initializing, isAlive,
				ipEndpoint, ipEndpoint, ipEndpoint, ipEndpoint, ipEndpoint, ipEndpoint,
				0, 0, 0, -1, -1, Guid.Empty, 0, false);
		}

		private static VNodeInfo NodeInfoFromMemberInfo(MemberInfo memberInfo) {
			return new VNodeInfo(memberInfo.InstanceId, 0, memberInfo.InternalTcpEndPoint,
				memberInfo.InternalSecureTcpEndPoint, memberInfo.ExternalTcpEndPoint,
				memberInfo.ExternalSecureTcpEndPoint, memberInfo.InternalHttpEndPoint, memberInfo.ExternalHttpEndPoint,
				memberInfo.IsReadOnlyReplica);
		}

		[Test]
		public void
			should_remove_dead_members_which_have_timestamps_older_than_the_allowed_dead_member_removal_timeout() {
			var timeProvider = new FakeTimeProvider();
			var deadMemberRemovalTimeout = TimeSpan.FromSeconds(1);
			var allowedTimeDifference = TimeSpan.FromMilliseconds(1000);

			var me = TestNodeFor(1, isAlive: false, timeProvider.UtcNow);
			var nodeToBeRemoved =
				TestNodeFor(2, isAlive: false, timeProvider.UtcNow.Subtract(deadMemberRemovalTimeout));
			var peer = TestNodeFor(3, isAlive: false, timeProvider.UtcNow);
			var cluster = new ClusterInfo(me, nodeToBeRemoved, peer);

			var updatedCluster = GossipServiceBase.MergeClusters(
				cluster, cluster, me.InternalHttpEndPoint,
				info => info, timeProvider.UtcNow, NodeInfoFromMemberInfo(me), NodeInfoFromMemberInfo(peer),
				allowedTimeDifference, deadMemberRemovalTimeout);

			Assert.That(updatedCluster.Members, Has.Length.EqualTo(2));
			Assert.IsFalse(updatedCluster.Members.Any(x => x.Is(nodeToBeRemoved.InternalHttpEndPoint)));
		}

		[Test]
		public void
			should_not_remove_alive_members_which_have_timestamps_older_than_the_allowed_dead_member_removal_timeout() {
			var timeProvider = new FakeTimeProvider();
			var deadMemberRemovalTimeout = TimeSpan.FromSeconds(1);
			var allowedTimeDifference = TimeSpan.FromMilliseconds(1000);

			var me = TestNodeFor(1, isAlive: true, timeProvider.UtcNow);
			var nodeToBeRemoved = TestNodeFor(2, isAlive: true, timeProvider.UtcNow.Subtract(deadMemberRemovalTimeout));
			var peer = TestNodeFor(3, isAlive: true, timeProvider.UtcNow);
			var cluster = new ClusterInfo(me, nodeToBeRemoved, peer);

			var updatedCluster = GossipServiceBase.MergeClusters(
				cluster, cluster, me.InternalHttpEndPoint,
				info => info, timeProvider.UtcNow, NodeInfoFromMemberInfo(me), NodeInfoFromMemberInfo(peer),
				allowedTimeDifference, deadMemberRemovalTimeout);

			Assert.That(updatedCluster.Members, Has.Length.EqualTo(3));
			Assert.IsTrue(updatedCluster.Members.Any(x => x.Is(nodeToBeRemoved.InternalHttpEndPoint)));
		}
	}
}
