using System;
using System.Linq;
using System.Net;
using EventStore.Core.Bus;
using EventStore.Core.Cluster;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Infrastructure;
using EventStore.Core.TransactionLog.Checkpoint;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.ElectionsService {
	public abstract class ElectionsFixture {
		protected readonly VNodeInfo _node;
		protected readonly VNodeInfo _nodeTwo;
		protected readonly VNodeInfo _nodeThree;
		public Func<DateTime> GetUtcNow;
		public Core.Services.ElectionsService SUT;
		protected IBus _bus;
		protected FakePublisher _publisher;
		protected Guid _epochId;

		protected static Func<int, bool, VNodeInfo> NodeFactory = (id, isReadOnlyReplica) => new VNodeInfo(
			Guid.Parse($"00000000-0000-0000-0000-00000000000{id}"), id,
			new IPEndPoint(IPAddress.Loopback, id),
			new IPEndPoint(IPAddress.Loopback, id),
			new IPEndPoint(IPAddress.Loopback, id),
			new IPEndPoint(IPAddress.Loopback, id),
			new IPEndPoint(IPAddress.Loopback, id),
			new IPEndPoint(IPAddress.Loopback, id), isReadOnlyReplica);

		protected static readonly Func<VNodeInfo, DateTime, VNodeState, bool, Guid, MemberInfo> MemberInfoFromVNode =
			(nodeInfo, timestamp, state, isAlive, epochId) => MemberInfo.ForVNode(
				nodeInfo.InstanceId, timestamp, state, isAlive,
				nodeInfo.InternalTcp,
				nodeInfo.InternalSecureTcp, nodeInfo.ExternalTcp, nodeInfo.ExternalSecureTcp,
				nodeInfo.InternalHttp,
				nodeInfo.ExternalHttp, 0, 0, 0, 0, 0, epochId, 0,
				nodeInfo.IsReadOnlyReplica);

		protected ElectionsFixture(VNodeInfo node, VNodeInfo nodeTwo, VNodeInfo nodeThree) {
			var now = DateTime.UtcNow;
			GetUtcNow = () => now;
			_publisher = new FakePublisher();
			_bus = new InMemoryBus("Test");
			_epochId = Guid.NewGuid();

			_node = node;
			_nodeTwo = nodeTwo;
			_nodeThree = nodeThree;
			SUT = new Core.Services.ElectionsService(_publisher, _node, 3,
				new InMemoryCheckpoint(0),
				new InMemoryCheckpoint(0),
				new FakeEpochManager(), () => 0L, 0, GetUtcNow);

			SUT.SubscribeMessages(_bus);
		}
	}

	public class when_starting_elections : ElectionsFixture {
		public when_starting_elections()
			: base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_send_view_change_to_other_members() {
			SUT.Handle(new ElectionMessage.StartElections());

			var expected = new Message[] {
				TimerMessage.Schedule.Create(
					Core.Services.ElectionsService.SendViewChangeProofInterval,
					new PublishEnvelope(_publisher),
					new ElectionMessage.SendViewChangeProof()),
				TimerMessage.Schedule.Create(
					Core.Services.ElectionsService.LeaderElectionProgressTimeout,
					new PublishEnvelope(_publisher),
					new ElectionMessage.ElectionsTimedOut(0)),
				new HttpMessage.SendOverHttp(_nodeTwo.InternalHttp,
					new ElectionMessage.ViewChange(_node.InstanceId, _node.InternalHttp, 0),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout)),
				new HttpMessage.SendOverHttp(_nodeThree.InternalHttp,
					new ElectionMessage.ViewChange(_node.InstanceId, _node.InternalHttp, 0),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout)),
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}
	
	public class when_starting_elections_for_readonly_replica : ElectionsFixture {
		public when_starting_elections_for_readonly_replica()
			: base(NodeFactory(3, true), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_send_view_change_to_other_members() {
			SUT.Handle(new ElectionMessage.StartElections());

			Assert.Fail("TODO: Need to check if this is the correct behavior to continue.");
		}
	}

	public class when_starting_elections_after_elections_have_started : ElectionsFixture {
		public when_starting_elections_after_elections_have_started()
			: base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_starting_elections() {
			SUT.Handle(new ElectionMessage.StartElections());
			_publisher.Messages.Clear();
			SUT.Handle(new ElectionMessage.StartElections());

			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_node_is_shutting_down_and_starting_elections : ElectionsFixture {
		public when_node_is_shutting_down_and_starting_elections()
			: base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_starting_elections() {
			SUT.Handle(new SystemMessage.BecomeShuttingDown(Guid.NewGuid(), false, false));
			SUT.Handle(new ElectionMessage.StartElections());

			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_node_is_shutting_down_and_elections_timed_out : ElectionsFixture {
		public when_node_is_shutting_down_and_elections_timed_out() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_elections_timed_out() {
			SUT.Handle(new ElectionMessage.StartElections());
			_publisher.Messages.Clear();
			SUT.Handle(new SystemMessage.BecomeShuttingDown(Guid.NewGuid(), false, false));
			SUT.Handle(new ElectionMessage.ElectionsTimedOut(0));

			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_view_change_proof_is_triggered_and_the_first_election_has_not_completed : ElectionsFixture {
		public when_view_change_proof_is_triggered_and_the_first_election_has_not_completed() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_schedule_another_view_change_proof() {
			SUT.Handle(new ElectionMessage.StartElections());
			_publisher.Messages.Clear();
			SUT.Handle(new ElectionMessage.SendViewChangeProof());

			var expected = new Message[] {
				TimerMessage.Schedule.Create(
					Core.Services.ElectionsService.SendViewChangeProofInterval,
					new PublishEnvelope(_publisher),
					new ElectionMessage.SendViewChangeProof()),
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}

	public class when_view_change_proof_is_triggered_and_the_first_election_has_completed : ElectionsFixture {
		public when_view_change_proof_is_triggered_and_the_first_election_has_completed() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_send_view_change_proof_to_other_members() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			SUT.Handle(new ElectionMessage.PrepareOk(0, _nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0, 0,
				_epochId, 0, 0, 0, 0));
			var proposalHttpMessage = _publisher.Messages.OfType<HttpMessage.SendOverHttp>()
				.FirstOrDefault(x => x.Message is ElectionMessage.Proposal);
			var proposalMessage = (ElectionMessage.Proposal)proposalHttpMessage.Message;
			_publisher.Messages.Clear();
			SUT.Handle(new ElectionMessage.Accept(_nodeTwo.InstanceId, _nodeTwo.InternalHttp,
				proposalMessage.MasterId, proposalMessage.MasterInternalHttp, 0));
			_publisher.Messages.Clear();
			SUT.Handle(new ElectionMessage.SendViewChangeProof());

			var expected = new Message[] {
				TimerMessage.Schedule.Create(
					Core.Services.ElectionsService.SendViewChangeProofInterval,
					new PublishEnvelope(_publisher),
					new ElectionMessage.SendViewChangeProof()),
				new HttpMessage.SendOverHttp(_nodeTwo.InternalHttp,
					new ElectionMessage.ViewChangeProof(_node.InstanceId, _node.InternalHttp, 0),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout)),
				new HttpMessage.SendOverHttp(_nodeThree.InternalHttp,
					new ElectionMessage.ViewChangeProof(_node.InstanceId, _node.InternalHttp, 0),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout))
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}

	public class when_receiving_view_change_and_shutting_down : ElectionsFixture {
		public when_receiving_view_change_and_shutting_down() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_the_view_change() {
			_publisher.Messages.Clear();

			SUT.Handle(new SystemMessage.BecomeShuttingDown(Guid.NewGuid(), false, false));
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, -2));

			Assert.IsEmpty(_publisher.Messages);
		}
	}
	
	public class when_receiving_view_change_and_idle : ElectionsFixture {
		public when_receiving_view_change_and_idle() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_the_view_change() {
			_publisher.Messages.Clear();

			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, -2));

			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_receiving_view_change_after_node_is_shutting_down : ElectionsFixture {
		public when_receiving_view_change_after_node_is_shutting_down() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_the_view_change() {
			SUT.Handle(new ElectionMessage.StartElections());
			_publisher.Messages.Clear();

			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, -2));

			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_receiving_view_change_for_an_earlier_view_than_installed : ElectionsFixture {
		public when_receiving_view_change_for_an_earlier_view_than_installed() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_the_view_change() {
			SUT.Handle(new ElectionMessage.StartElections());
			_publisher.Messages.Clear();

			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, -2));

			Assert.IsEmpty(_publisher.Messages);
		}
	}
	
	public class when_receiving_a_view_change_for_a_later_view_than_last_attempted_view : ElectionsFixture {
		public when_receiving_a_view_change_for_a_later_view_than_last_attempted_view() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_send_view_change_to_other_members() {
			SUT.Handle(new ElectionMessage.StartElections());
			_publisher.Messages.Clear();

			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 10));
			
			var expected = new Message[] {
				TimerMessage.Schedule.Create(
					Core.Services.ElectionsService.LeaderElectionProgressTimeout,
					new PublishEnvelope(_publisher),
					new ElectionMessage.ElectionsTimedOut(10)),
				new HttpMessage.SendOverHttp(_nodeTwo.InternalHttp,
					new ElectionMessage.ViewChange(_node.InstanceId, _node.InternalHttp, 10),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout)),
				new HttpMessage.SendOverHttp(_nodeThree.InternalHttp,
					new ElectionMessage.ViewChange(_node.InstanceId, _node.InternalHttp, 10),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout)),
			};
			
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}

	public class when_receiving_view_change_from_majority_and_not_the_leader_of_the_current_view : ElectionsFixture {
		public when_receiving_view_change_from_majority_and_not_the_leader_of_the_current_view() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_not_initiate_prepare_phase() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ElectionsTimedOut(0));
			_publisher.Messages.Clear();

			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));

			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_receiving_view_change_from_majority : ElectionsFixture {
		public when_receiving_view_change_from_majority() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_send_prepares_to_other_members() {
			SUT.Handle(new ElectionMessage.StartElections());
			_publisher.Messages.Clear();

			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));

			var expected = new[] {
				new HttpMessage.SendOverHttp(_nodeTwo.InternalHttp,
					new ElectionMessage.Prepare(_node.InstanceId, _node.InternalHttp, 0),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout)),
				new HttpMessage.SendOverHttp(_nodeThree.InternalHttp,
					new ElectionMessage.Prepare(_node.InstanceId, _node.InternalHttp, 0),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout))
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}

	public class when_receiving_a_prepare : ElectionsFixture {
		public when_receiving_a_prepare() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_reply_with_prepare_ok() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			_publisher.Messages.Clear();
			SUT.Handle(new ElectionMessage.Prepare(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));

			var expected = new Message[] {
				new HttpMessage.SendOverHttp(_nodeTwo.InternalHttp,
					new ElectionMessage.PrepareOk(0,
						_node.InstanceId, _node.InternalHttp, -1, -1, Guid.Empty, 0, 0, 0, 0),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout)),
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}

	public class when_receiving_a_prepare_when_node_is_shutting_down : ElectionsFixture {
		public when_receiving_a_prepare_when_node_is_shutting_down() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_the_prepare() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			SUT.Handle(new ElectionMessage.Prepare(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			_publisher.Messages.Clear();
			SUT.Handle(new SystemMessage.BecomeShuttingDown(Guid.NewGuid(), false, false));
			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_receiving_a_prepare_for_the_same_node : ElectionsFixture {
		public when_receiving_a_prepare_for_the_same_node() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_the_prepare() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			_publisher.Messages.Clear();
			SUT.Handle(new ElectionMessage.Prepare(_node.InstanceId, _node.InternalHttp, 0));
			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_receiving_a_prepare_not_for_the_last_attempted_view : ElectionsFixture {
		public when_receiving_a_prepare_not_for_the_last_attempted_view() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_the_prepare() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			_publisher.Messages.Clear();
			SUT.Handle(new ElectionMessage.Prepare(_nodeThree.InstanceId, _nodeThree.InternalHttp, 1));
			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_receiving_a_prepare_from_an_unknown_node : ElectionsFixture {
		public when_receiving_a_prepare_from_an_unknown_node() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_the_prepare() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			_publisher.Messages.Clear();
			SUT.Handle(new ElectionMessage.Prepare(Guid.NewGuid(), new IPEndPoint(IPAddress.Loopback, 1114), 0));
			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_receiving_a_prepare_for_a_readonly_replica : ElectionsFixture {
		public when_receiving_a_prepare_for_a_readonly_replica() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_the_prepare() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			_publisher.Messages.Clear();
			SUT.Handle(new ElectionMessage.Prepare(_node.InstanceId, _node.InternalHttp, 0));
			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_receiving_prepare_ok_and_node_is_shutting_down : ElectionsFixture {
		public when_receiving_prepare_ok_and_node_is_shutting_down() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_prepare_ok() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			_publisher.Messages.Clear();
			SUT.Handle(new SystemMessage.BecomeShuttingDown(Guid.NewGuid(), false, false));

			SUT.Handle(new ElectionMessage.PrepareOk(0, _nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0, 0,
				_epochId, 0, 0, 0, 0));

			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_receiving_prepare_ok_and_elections_have_not_started : ElectionsFixture {
		public when_receiving_prepare_ok_and_elections_have_not_started() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_prepare_ok() {
			SUT.Handle(new ElectionMessage.PrepareOk(0, _nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0, 0,
				_epochId, 0, 0, 0, 0));

			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_receiving_prepare_ok_for_not_the_current_attempted_view : ElectionsFixture {
		public when_receiving_prepare_ok_for_not_the_current_attempted_view() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_prepare_ok() {
			SUT.Handle(new ElectionMessage.StartElections());
			_publisher.Messages.Clear();
			SUT.Handle(new ElectionMessage.PrepareOk(-1, _nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0, 0,
				_epochId, 0, 0, 0, 0));

			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_receiving_a_duplicate_prepare : ElectionsFixture {
		public when_receiving_a_duplicate_prepare() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_prepare_ok() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			_publisher.Messages.Clear();

			SUT.Handle(new ElectionMessage.PrepareOk(0, _node.InstanceId, _node.InternalHttp, 0, 0,
				_epochId, 0, 0, 0, 0));

			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_receiving_majority_prepare_ok : ElectionsFixture {
		public when_receiving_majority_prepare_ok() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_send_proposal_to_other_members() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			_publisher.Messages.Clear();

			SUT.Handle(new ElectionMessage.PrepareOk(0, _nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0, 0,
				_epochId, 0, 0, 0, 0));

			var proposalHttpMessage = _publisher.Messages.OfType<HttpMessage.SendOverHttp>()
				.FirstOrDefault(x => x.Message is ElectionMessage.Proposal);
			var proposalMessage = (ElectionMessage.Proposal)proposalHttpMessage.Message;

			var expected = new[] {
				new HttpMessage.SendOverHttp(_nodeTwo.InternalHttp,
					new ElectionMessage.Proposal(_node.InstanceId, _node.InternalHttp,
						proposalMessage.MasterId,
						proposalMessage.MasterInternalHttp, 0, 0, 0, _epochId, 0, 0, 0, 0),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout)),
				new HttpMessage.SendOverHttp(_nodeThree.InternalHttp,
					new ElectionMessage.Proposal(_node.InstanceId, _node.InternalHttp,
						proposalMessage.MasterId,
						proposalMessage.MasterInternalHttp, 0, 0, 0, _epochId, 0, 0, 0, 0),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout))
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}

	public class when_node_is_shutting_down_and_receive_a_proposal : ElectionsFixture {
		public when_node_is_shutting_down_and_receive_a_proposal() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_proposal() {
			SUT.Handle(new SystemMessage.BecomeShuttingDown(Guid.NewGuid(), false, false));
			SUT.Handle(new ElectionMessage.Proposal(_node.InstanceId, _node.InternalHttp,
				_node.InstanceId,
				_node.InternalHttp, 0, 0, 0, _epochId, 0, 0, 0, 0));

			Assert.IsEmpty(_publisher.Messages);
		}
	}
	
	public class when_receiving_a_proposal_from_the_same_node : ElectionsFixture {
		public when_receiving_a_proposal_from_the_same_node() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_proposal() {
			SUT.Handle(new ElectionMessage.Proposal(_node.InstanceId, _node.InternalHttp,
				_node.InstanceId,
				_node.InternalHttp, 0, 0, 0, _epochId, 0, 0, 0, 0));

			Assert.IsEmpty(_publisher.Messages, "Nodes do not send proposals to themselves, they accept their own proposal implicitly.");
		}
	}
	
	public class when_receiving_a_proposal_and_an_acceptor_of_the_current_view : ElectionsFixture {
		public when_receiving_a_proposal_and_an_acceptor_of_the_current_view() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_proposal() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			SUT.Handle(new ElectionMessage.PrepareOk(0, _nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0, 0,
				_epochId, 0, 0, 0, 0));
			_publisher.Messages.Clear();
			SUT.Handle(new ElectionMessage.Proposal(_nodeTwo.InstanceId, _nodeTwo.InternalHttp,
				_node.InstanceId,
				_node.InternalHttp, 0, 0, 0, _epochId, 0, 0, 0, 0));

			Assert.IsEmpty(_publisher.Messages);
		}
	}
	
	public class when_receiving_a_proposal_not_for_the_current_installed_view : ElectionsFixture {
		public when_receiving_a_proposal_not_for_the_current_installed_view() :
			base(NodeFactory(1, false), NodeFactory(2, false), NodeFactory(3, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_proposal() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			SUT.Handle(new ElectionMessage.Prepare(_nodeThree.InstanceId, _nodeThree.InternalHttp, 0));
			_publisher.Messages.Clear();
			SUT.Handle(new ElectionMessage.Proposal(_nodeTwo.InstanceId, _nodeTwo.InternalHttp,
				_node.InstanceId,
				_node.InternalHttp, 1, 0, 0, _epochId, 0, 0, 0, 0));

			Assert.IsEmpty(_publisher.Messages);
		}
	}
	
	public class when_receiving_a_proposal_from_an_unknown_node : ElectionsFixture {
		public when_receiving_a_proposal_from_an_unknown_node() :
			base(NodeFactory(1, false), NodeFactory(2, false), NodeFactory(3, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_proposal() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			SUT.Handle(new ElectionMessage.Prepare(_nodeThree.InstanceId, _nodeThree.InternalHttp, 0));
			_publisher.Messages.Clear();
			SUT.Handle(new ElectionMessage.Proposal(Guid.NewGuid(), new IPEndPoint(IPAddress.Loopback, 4), 
				_node.InstanceId,
				_node.InternalHttp, 0, 0, 0, _epochId, 0, 0, 0, 0));

			Assert.IsEmpty(_publisher.Messages);
		}
	}
	
	public class when_receiving_a_proposal_for_an_unknown_node : ElectionsFixture {
		public when_receiving_a_proposal_for_an_unknown_node() :
			base(NodeFactory(1, false), NodeFactory(2, false), NodeFactory(3, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_proposal() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			SUT.Handle(new ElectionMessage.Prepare(_nodeThree.InstanceId, _nodeThree.InternalHttp, 0));
			_publisher.Messages.Clear();
			SUT.Handle(new ElectionMessage.Proposal(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 
				Guid.NewGuid(),
				new IPEndPoint(IPAddress.Loopback, 4), 0, 0, 0, _epochId, 0, 0, 0, 0));

			Assert.IsEmpty(_publisher.Messages);
		}
	}
	
	public class when_receiving_a_proposal : ElectionsFixture {
		public when_receiving_a_proposal() :
			base(NodeFactory(1, false), NodeFactory(2, false), NodeFactory(3, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_send_an_acceptance_to_other_members() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			SUT.Handle(new ElectionMessage.Prepare(_nodeThree.InstanceId, _nodeThree.InternalHttp, 0));
			_publisher.Messages.Clear();
			SUT.Handle(new ElectionMessage.Proposal(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 
				_nodeThree.InstanceId,
				_nodeThree.InternalHttp, 0, 0, 0, _epochId, 0, 0, 0, 0));

			var expected = new Message[] {
				new HttpMessage.SendOverHttp(_nodeTwo.InternalHttp,
					new ElectionMessage.Accept(_node.InstanceId, _node.InternalHttp,
						_nodeThree.InstanceId,
						_nodeThree.InternalHttp, 0),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout)),
				new HttpMessage.SendOverHttp(_nodeThree.InternalHttp,
					new ElectionMessage.Accept(_node.InstanceId, _node.InternalHttp,
						_nodeThree.InstanceId,
						_nodeThree.InternalHttp, 0), 
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout)),
				new ElectionMessage.ElectionsDone(0,
					MemberInfo.ForVNode(
						_nodeThree.InstanceId, GetUtcNow(), VNodeState.Unknown, true,
						_nodeThree.InternalTcp,
						_nodeThree.InternalSecureTcp, _nodeThree.ExternalTcp, _nodeThree.ExternalSecureTcp,
						_nodeThree.InternalHttp,
						_nodeThree.ExternalHttp, 0, 0, 0, 0, 0, _epochId, 0,
						_nodeThree.IsReadOnlyReplica)),
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}

	public class when_receiving_majority_accept : ElectionsFixture {
		public when_receiving_majority_accept() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_complete_elections() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			SUT.Handle(new ElectionMessage.PrepareOk(0, _nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0, 0,
				_epochId, 0, 0, 0, 0));
			var proposalHttpMessage = _publisher.Messages.OfType<HttpMessage.SendOverHttp>()
				.FirstOrDefault(x => x.Message is ElectionMessage.Proposal);
			var proposalMessage = (ElectionMessage.Proposal)proposalHttpMessage.Message;
			_publisher.Messages.Clear();

			SUT.Handle(new ElectionMessage.Accept(_nodeTwo.InstanceId, _nodeTwo.InternalHttp,
				proposalMessage.MasterId, proposalMessage.MasterInternalHttp, 0));

			var expected = new[] {
				new ElectionMessage.ElectionsDone(0,
					MemberInfo.ForVNode(
						_nodeTwo.InstanceId, GetUtcNow(), VNodeState.Unknown, true,
						_nodeTwo.InternalTcp,
						_nodeTwo.InternalSecureTcp, _nodeTwo.ExternalTcp, _nodeTwo.ExternalSecureTcp,
						_nodeTwo.InternalHttp,
						_nodeTwo.ExternalHttp, 0, 0, 0, 0, 0, _epochId, 0,
						_nodeTwo.IsReadOnlyReplica)),
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}

	public class when_elections_timeout_and_not_electing_leader : ElectionsFixture {
		public when_elections_timeout_and_not_electing_leader() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_timeout() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			SUT.Handle(new ElectionMessage.PrepareOk(0, _nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0, 0,
				_epochId, 0, 0, 0, 0));
			var proposalHttpMessage = _publisher.Messages.OfType<HttpMessage.SendOverHttp>()
				.FirstOrDefault(x => x.Message is ElectionMessage.Proposal);
			var proposalMessage = (ElectionMessage.Proposal)proposalHttpMessage.Message;
			SUT.Handle(new ElectionMessage.Accept(_nodeTwo.InstanceId, _nodeTwo.InternalHttp,
				proposalMessage.MasterId, proposalMessage.MasterInternalHttp, 0));
			_publisher.Messages.Clear();
			SUT.Handle(new ElectionMessage.ElectionsTimedOut(0));

			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_updating_node_priority : ElectionsFixture {
		public when_updating_node_priority() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_broadcast_node_priority_update() {
			var nodePriority = 5;
			SUT.Handle(new ClientMessage.SetNodePriority(nodePriority));

			var expected = new[] {
				new GossipMessage.UpdateNodePriority(nodePriority)
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}

	public class when_resigning_node_and_node_is_not_the_current_master : ElectionsFixture {
		public when_resigning_node_and_node_is_not_the_current_master() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_ignore_resign_node_message() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			SUT.Handle(new ElectionMessage.PrepareOk(0, _nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0, 0,
				_epochId, 0, 0, 0, 0));
			var proposalHttpMessage = _publisher.Messages.OfType<HttpMessage.SendOverHttp>()
				.FirstOrDefault(x => x.Message is ElectionMessage.Proposal);
			var proposalMessage = (ElectionMessage.Proposal)proposalHttpMessage.Message;
			SUT.Handle(new ElectionMessage.Accept(_nodeTwo.InstanceId, _nodeTwo.InternalHttp,
				proposalMessage.MasterId, proposalMessage.MasterInternalHttp, 0));
			_publisher.Messages.Clear();
			SUT.Handle(new ClientMessage.ResignNode());

			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_resigning_node_and_node_is_the_current_master : ElectionsFixture {
		public when_resigning_node_and_node_is_the_current_master() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_initiate_master_resignation_and_inform_other_nodes() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			SUT.Handle(new ElectionMessage.PrepareOk(0, _nodeTwo.InstanceId, _nodeTwo.InternalHttp, -1, 0,
				_epochId, -1, -1, -1, -1));
			SUT.Handle(new ElectionMessage.Accept(_nodeTwo.InstanceId, _nodeTwo.InternalHttp,
				_node.InstanceId, _node.InternalHttp, 0));
			_publisher.Messages.Clear();

			SUT.Handle(new ClientMessage.ResignNode());

			var expected = new Message[] {
				new HttpMessage.SendOverHttp(_nodeTwo.InternalHttp,
					new ElectionMessage.MasterIsResigning(_node.InstanceId, _node.InternalHttp),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout)),
				new HttpMessage.SendOverHttp(_nodeThree.InternalHttp,
					new ElectionMessage.MasterIsResigning(_node.InstanceId, _node.InternalHttp),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout)),
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}

	public class when_resigning_node_and_majority_resigning_ok_received : ElectionsFixture {
		public when_resigning_node_and_majority_resigning_ok_received() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_initiate_master_resignation() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			SUT.Handle(new ElectionMessage.PrepareOk(0, _nodeTwo.InstanceId, _nodeTwo.InternalHttp, -1, 0,
				_epochId, -1, -1, -1, -1));
			SUT.Handle(new ElectionMessage.Accept(_nodeTwo.InstanceId, _nodeTwo.InternalHttp,
				_node.InstanceId, _node.InternalHttp, 0));
			SUT.Handle(new ClientMessage.ResignNode());
			_publisher.Messages.Clear();

			SUT.Handle(new ElectionMessage.MasterIsResigningOk(
				_node.InstanceId,
				_node.InternalHttp,
				_nodeTwo.InstanceId,
				_nodeTwo.InternalHttp));

			var expected = new Message[] {
				new SystemMessage.InitiateMasterResignation(),
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}

	public class when_electing_a_master_and_master_node_resigned : ElectionsFixture {
		public when_electing_a_master_and_master_node_resigned() :
			base(NodeFactory(3, false), NodeFactory(2, false), NodeFactory(1, false)) {
			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfoFromVNode(_node, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeTwo, GetUtcNow(), VNodeState.Unknown, true, _epochId),
				MemberInfoFromVNode(_nodeThree, GetUtcNow(), VNodeState.Unknown, true, _epochId))));
		}

		[Test]
		public void should_attempt_not_to_elect_previously_elected_master() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			SUT.Handle(new ElectionMessage.PrepareOk(0, _nodeTwo.InstanceId, _nodeTwo.InternalHttp, -1, 0,
				_epochId, -1, -1, -1, -1));
			SUT.Handle(new ElectionMessage.Accept(_nodeTwo.InstanceId, _nodeTwo.InternalHttp,
				_nodeThree.InstanceId, _nodeThree.InternalHttp, 0));
			_publisher.Messages.Clear();

			SUT.Handle(new ClientMessage.ResignNode());
			SUT.Handle(new ElectionMessage.MasterIsResigningOk(
				_node.InstanceId,
				_node.InternalHttp,
				_nodeTwo.InstanceId,
				_nodeTwo.InternalHttp));
			_publisher.Messages.Clear();

			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 3));
			SUT.Handle(new ElectionMessage.PrepareOk(3, _nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0, 0,
				_epochId, 0, 0, 0, 0));
			var proposalHttpMessage = _publisher.Messages.OfType<HttpMessage.SendOverHttp>()
				.FirstOrDefault(x => x.Message is ElectionMessage.Proposal);
			var proposalMessage = (ElectionMessage.Proposal)proposalHttpMessage.Message;
			_publisher.Messages.Clear();
			SUT.Handle(new ElectionMessage.Accept(_nodeTwo.InstanceId, _nodeTwo.InternalHttp,
				proposalMessage.MasterId, proposalMessage.MasterInternalHttp, 3));

			var expected = new[] {
				new ElectionMessage.ElectionsDone(3,
					MemberInfo.ForVNode(
						_nodeTwo.InstanceId, GetUtcNow(), VNodeState.Unknown, true,
						_nodeTwo.InternalTcp,
						_nodeTwo.InternalSecureTcp, _nodeTwo.ExternalTcp, _nodeTwo.ExternalSecureTcp,
						_nodeTwo.InternalHttp,
						_nodeTwo.ExternalHttp, 0, 0, 0, 0, 0, _epochId, 0,
						_nodeTwo.IsReadOnlyReplica)),
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}
}
