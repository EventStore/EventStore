using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitReplication {
	[TestFixture]
	public class
		when_3_node_cluster_receives_multiple_acks_for_different_positions_out_of_order : with_index_committer_service {
		private CountdownEvent _eventsReplicated = new CountdownEvent(2);

		private Guid _correlationId1 = Guid.NewGuid();
		private Guid _correlationId2 = Guid.NewGuid();

		private long _logPosition1 = 1000;
		private long _logPosition2 = 2000;

		public override void Given() { }

		public override void When() {
			Publisher.Subscribe(new AdHocHandler<StorageMessage.CommitIndexed>(m => _eventsReplicated.Signal()));
			BecomeMaster();
			AddPendingPrepare(_logPosition1, publishChaserMsgs: false);
			AddPendingPrepare(_logPosition2, publishChaserMsgs: false);
			Service.Handle(new StorageMessage.CommitAck(_correlationId2, _logPosition2, _logPosition2, 0, 0));
			Service.Handle(new StorageMessage.CommitAck(_correlationId1, _logPosition1, _logPosition1, 0, 0));
			
			// Reach quorum for logPosition2			
			CommitTracker.Handle(new CommitMessage.WrittenTo(_logPosition2));
			CommitTracker.Handle(new CommitMessage.ReplicaWrittenTo(_logPosition2, Guid.NewGuid()));

			if (!_eventsReplicated.Wait(TimeSpan.FromSeconds(TimeoutSeconds))) {
				Assert.Fail("Timed out waiting for commit replicated messages to be published");
			}
		}

		[Test]
		public void replication_checkpoint_should_have_been_updated() {
			Assert.AreEqual(_logPosition2, ReplicationCheckpoint.ReadNonFlushed());
		}

		[Test]
		public void commit_replicated_message_should_have_been_published_for_first_two_events() {
			Assert.AreEqual(2, CommitReplicatedMgs.Count);
			Assert.AreEqual(_correlationId1, CommitReplicatedMgs[0].CorrelationId);
			Assert.AreEqual(_correlationId2, CommitReplicatedMgs[1].CorrelationId);
		}
		[Test]
		public void index_written_message_should_have_been_published_for_first_two_events() {
			Assert.AreEqual(2, IndexWrittenMgs.Count);
			Assert.AreEqual(_logPosition1, IndexWrittenMgs[0].LogPosition);
			Assert.AreEqual(_logPosition2, IndexWrittenMgs[1].LogPosition);
		}
		[Test]
		public void index_should_have_been_updated() {
			Assert.AreEqual(2, IndexCommitter.CommittedPrepares.Count);
			Assert.AreEqual(_logPosition1, IndexCommitter.CommittedPrepares[0].LogPosition);
			Assert.AreEqual(_logPosition2, IndexCommitter.CommittedPrepares[1].LogPosition);
		}
	}
}
