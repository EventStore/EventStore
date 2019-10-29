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
		public Func<DateTime> GetUtcNow;
		public Core.Services.ElectionsService SUT;
		protected IBus _bus;
		protected FakePublisher _publisher;
		protected VNodeInfo _nodeOne;
		protected VNodeInfo _nodeTwo;
		protected VNodeInfo _nodeThree;
		protected Guid _epochId;

		protected ElectionsFixture() {
			var now = DateTime.UtcNow;
			GetUtcNow = () => now;
			_publisher = new FakePublisher();
			_bus = new InMemoryBus("Test");
			_epochId = Guid.NewGuid();

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

			SUT = new Core.Services.ElectionsService(_publisher, _nodeThree, 3,
				new InMemoryCheckpoint(0),
				new InMemoryCheckpoint(0),
				new FakeEpochManager(), () => 0L, 0, GetUtcNow);

			SUT.SubscribeMessages(_bus);

			SUT.Handle(new GossipMessage.GossipUpdated(new ClusterInfo(
				MemberInfo.ForVNode(
					_nodeOne.InstanceId, GetUtcNow(), VNodeState.Unknown, true,
					_nodeOne.InternalTcp,
					_nodeOne.InternalSecureTcp, _nodeOne.ExternalTcp, _nodeOne.ExternalSecureTcp,
					_nodeOne.InternalHttp,
					_nodeOne.ExternalHttp, 0, 0, 0, 0, 0, _epochId, 0,
					_nodeOne.IsReadOnlyReplica),
				MemberInfo.ForVNode(
					_nodeTwo.InstanceId, GetUtcNow(), VNodeState.Unknown, true,
					_nodeTwo.InternalTcp,
					_nodeTwo.InternalSecureTcp, _nodeTwo.ExternalTcp, _nodeTwo.ExternalSecureTcp,
					_nodeTwo.InternalHttp,
					_nodeTwo.ExternalHttp, 0, 0, 0, 0, 0, _epochId, 0,
					_nodeTwo.IsReadOnlyReplica),
				MemberInfo.ForVNode(
					_nodeThree.InstanceId, GetUtcNow(), VNodeState.Unknown, true,
					_nodeThree.InternalTcp,
					_nodeThree.InternalSecureTcp, _nodeThree.ExternalTcp, _nodeThree.ExternalSecureTcp,
					_nodeThree.InternalHttp,
					_nodeThree.ExternalHttp, 0, 0, 0, 0, 0, _epochId, 0,
					_nodeThree.IsReadOnlyReplica))));
		}
	}

	public class when_starting_elections : ElectionsFixture {
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
				new HttpMessage.SendOverHttp(_nodeOne.InternalHttp,
					new ElectionMessage.ViewChange(_nodeThree.InstanceId, _nodeThree.InternalHttp, 0),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout)),
				new HttpMessage.SendOverHttp(_nodeTwo.InternalHttp,
					new ElectionMessage.ViewChange(_nodeThree.InstanceId, _nodeThree.InternalHttp, 0),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout))
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}

	public class when_starting_elections_after_elections_have_started : ElectionsFixture {
		[Test]
		public void should_ignore_starting_elections() {
			SUT.Handle(new ElectionMessage.StartElections());
			_publisher.Messages.Clear();
			SUT.Handle(new ElectionMessage.StartElections());

			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_node_is_shutting_down_and_starting_elections : ElectionsFixture {
		[Test]
		public void should_ignore_starting_elections() {
			SUT.Handle(new SystemMessage.BecomeShuttingDown(Guid.NewGuid(), false, false));
			SUT.Handle(new ElectionMessage.StartElections());

			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_node_is_shutting_down_and_elections_timed_out : ElectionsFixture {
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
				new HttpMessage.SendOverHttp(_nodeOne.InternalHttp,
					new ElectionMessage.ViewChangeProof(_nodeThree.InstanceId, _nodeThree.InternalHttp, 0),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout)),
				new HttpMessage.SendOverHttp(_nodeTwo.InternalHttp,
					new ElectionMessage.ViewChangeProof(_nodeThree.InstanceId, _nodeThree.InternalHttp, 0),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout))
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}

	public class when_receiving_view_change_and_idle : ElectionsFixture {
		[Test]
		public void should_ignore_the_view_change() {
			_publisher.Messages.Clear();

			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, -2));

			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_receiving_view_change_after_node_is_shutting_down : ElectionsFixture {
		[Test]
		public void should_ignore_the_view_change() {
			SUT.Handle(new ElectionMessage.StartElections());
			_publisher.Messages.Clear();

			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, -2));

			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_receiving_view_change_for_an_earlier_view_than_installed : ElectionsFixture {
		[Test]
		public void should_ignore_the_view_change() {
			SUT.Handle(new ElectionMessage.StartElections());
			_publisher.Messages.Clear();

			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, -2));

			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_receiving_view_change_from_majority_and_not_the_leader_of_the_current_view : ElectionsFixture {
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
		[Test]
		public void should_send_prepares_to_other_members() {
			SUT.Handle(new ElectionMessage.StartElections());
			_publisher.Messages.Clear();

			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));

			var expected = new[] {
				new HttpMessage.SendOverHttp(_nodeOne.InternalHttp,
					new ElectionMessage.Prepare(_nodeThree.InstanceId, _nodeThree.InternalHttp, 0),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout)),
				new HttpMessage.SendOverHttp(_nodeTwo.InternalHttp,
					new ElectionMessage.Prepare(_nodeThree.InstanceId, _nodeThree.InternalHttp, 0),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout))
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}

	public class when_receiving_prepare_ok_and_node_is_shutting_down : ElectionsFixture {
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
		[Test]
		public void should_ignore_prepare_ok() {
			SUT.Handle(new ElectionMessage.PrepareOk(0, _nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0, 0,
				_epochId, 0, 0, 0, 0));

			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_receiving_prepare_ok_for_not_the_current_attempted_view : ElectionsFixture {
		[Test]
		public void should_ignore_prepare_ok() {
			SUT.Handle(new ElectionMessage.PrepareOk(-1, _nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0, 0,
				_epochId, 0, 0, 0, 0));

			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_receiving_a_duplicate_prepare : ElectionsFixture {
		[Test]
		public void should_ignore_prepare_ok() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			_publisher.Messages.Clear();

			SUT.Handle(new ElectionMessage.PrepareOk(0, _nodeThree.InstanceId, _nodeThree.InternalHttp, 0, 0,
				_epochId, 0, 0, 0, 0));

			Assert.IsEmpty(_publisher.Messages);
		}
	}

	public class when_receiving_majority_prepare_ok : ElectionsFixture {
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
				new HttpMessage.SendOverHttp(_nodeOne.InternalHttp,
					new ElectionMessage.Proposal(_nodeThree.InstanceId, _nodeThree.InternalHttp,
						proposalMessage.MasterId,
						proposalMessage.MasterInternalHttp, 0, 0, 0, _epochId, 0, 0, 0, 0),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout)),
				new HttpMessage.SendOverHttp(_nodeTwo.InternalHttp,
					new ElectionMessage.Proposal(_nodeThree.InstanceId, _nodeThree.InternalHttp,
						proposalMessage.MasterId,
						proposalMessage.MasterInternalHttp, 0, 0, 0, _epochId, 0, 0, 0, 0),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout))
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}

	public class when_receiving_majority_accept : ElectionsFixture {
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

	public class when_updating_node_priority : ElectionsFixture {
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
		[Test]
		public void should_initiate_master_resignation_and_inform_other_nodes() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			SUT.Handle(new ElectionMessage.PrepareOk(0, _nodeTwo.InstanceId, _nodeTwo.InternalHttp, -1, 0,
				_epochId, -1, -1, -1, -1));
			SUT.Handle(new ElectionMessage.Accept(_nodeTwo.InstanceId, _nodeTwo.InternalHttp,
				_nodeThree.InstanceId, _nodeThree.InternalHttp, 0));
			_publisher.Messages.Clear();

			SUT.Handle(new ClientMessage.ResignNode()); 

			var expected = new Message[] {
				new HttpMessage.SendOverHttp(_nodeOne.InternalHttp,
					new ElectionMessage.MasterIsResigning(_nodeThree.InstanceId, _nodeThree.InternalHttp),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout)),
				new HttpMessage.SendOverHttp(_nodeTwo.InternalHttp,
					new ElectionMessage.MasterIsResigning(_nodeThree.InstanceId, _nodeThree.InternalHttp),
					GetUtcNow().Add(Core.Services.ElectionsService.LeaderElectionProgressTimeout)),
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}

	public class when_resigning_node_and_majority_resigning_ok_received : ElectionsFixture {
		[Test]
		public void should_initiate_master_resignation() {
			SUT.Handle(new ElectionMessage.StartElections());
			SUT.Handle(new ElectionMessage.ViewChange(_nodeTwo.InstanceId, _nodeTwo.InternalHttp, 0));
			SUT.Handle(new ElectionMessage.PrepareOk(0, _nodeTwo.InstanceId, _nodeTwo.InternalHttp, -1, 0,
				_epochId, -1, -1, -1, -1));
			SUT.Handle(new ElectionMessage.Accept(_nodeTwo.InstanceId, _nodeTwo.InternalHttp,
				_nodeThree.InstanceId, _nodeThree.InternalHttp, 0));
			SUT.Handle(new ClientMessage.ResignNode());
			_publisher.Messages.Clear();

			SUT.Handle(new ElectionMessage.MasterIsResigningOk(
				_nodeThree.InstanceId,
				_nodeThree.InternalHttp,
				_nodeTwo.InstanceId,
				_nodeTwo.InternalHttp));

			var expected = new Message[] {
				new SystemMessage.InitiateMasterResignation(),
			};
			Assert.That(_publisher.Messages, Is.EquivalentTo(expected).Using(ReflectionBasedEqualityComparer.Instance));
		}
	}

	public class when_electing_a_master_and_master_node_resigned : ElectionsFixture {
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
				_nodeThree.InstanceId,
				_nodeThree.InternalHttp,
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
