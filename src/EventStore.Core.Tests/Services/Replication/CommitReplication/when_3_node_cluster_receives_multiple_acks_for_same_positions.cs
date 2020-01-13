using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitReplication {
	[TestFixture]
	public class when_3_node_cluster_receives_multiple_acks_for_same_position : with_index_committer_service {

		private Guid _correlationId1 = Guid.NewGuid();

		private long _transactionId = 1000;
		private long _commitLogPosition = 2000;

		public override void Given() {
			BecomeMaster();
			AddPendingPrepare(_transactionId);
			Service.Handle(new StorageMessage.CommitAck(_correlationId1, _commitLogPosition, _transactionId, 0, 0));
			Service.Handle(new StorageMessage.CommitAck(_correlationId1, _commitLogPosition, _transactionId, 0, 0));

			Assert.AreEqual(0, CommitReplicatedMgs.Count, $"Recieved {CommitReplicatedMgs.Count} CommitReplicated messages during setup");
		}

		public int ReplicatedMsgCount => CommitReplicatedMgs.Count;

		public override void When() {
			Assert.Fail("Fix Test");
			//CommitTracker.Handle(new ReplicationTrackingMessage.WrittenTo(_commitLogPosition));
			//CommitTracker.Handle(new ReplicationTrackingMessage.ReplicaWrittenTo(_commitLogPosition, ReplicaId));
		}

		[Test]
		public void replication_checkpoint_should_have_been_updated() {
			AssertEx.IsOrBecomesTrue(() => _commitLogPosition == ReplicationCheckpoint.ReadNonFlushed());
		}

		[Test]
		public void commit_replicated_message_should_have_been_published_for_the_event() {
			AssertEx.IsOrBecomesTrue(() => 1 == CommitReplicatedMgs.Count, msg: $"Expected 1, but recieved {ReplicatedMsgCount} CommitReplicated messages");
		}
		[Test]
		public void index_written_message_should_have_been_published_for_the_event() {
			Assert.AreEqual(1, IndexWrittenMgs.Count);
			Assert.AreEqual(_commitLogPosition, IndexWrittenMgs[0].LogPosition);
		}

		[Test]
		public void index_should_have_been_updated() {
			Assert.AreEqual(1, IndexCommitter.CommittedPrepares.Count);
			Assert.AreEqual(_transactionId, IndexCommitter.CommittedPrepares[0].LogPosition);
		}
	}
}
