using System;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitReplication {
	[TestFixture]
	public class when_3_node_cluster_receives_2_replica_log_writes : with_index_committer_service {
		private long _logPosition = 4000;
		private Guid _correlationId = Guid.NewGuid();
		public override void When() {
			BecomeMaster();
			AddPendingPrepare(_logPosition);
			Service.Handle(new StorageMessage.CommitAck(_correlationId, _logPosition, _logPosition, 0, 0, true));
			Service.Handle(new StorageMessage.CommitAck(_correlationId, _logPosition, _logPosition, 0, 0));
			Publisher.Publish(new CommitMessage.ReplicatedTo(_logPosition));
			Publisher.Publish(new CommitMessage.ReplicaWrittenTo(_logPosition, Guid.NewGuid()));
		}

		[Test]
		public void replication_checkpoint_should_not_be_updated() {
			Assert.AreEqual(0, ReplicationCheckpoint.ReadNonFlushed());
		}

		[Test]
		public void commit_replicated_message_should_not_be_sent() {
			Assert.AreEqual(0, CommitReplicatedMgs.Count);
		}
		[Test]
		public void index_written_message_should_not_have_been_published() {
			Assert.AreEqual(0, IndexWrittenMgs.Count);
		}
		[Test]
		public void index_should_not_have_been_updated() {
			Assert.AreEqual(0, IndexCommitter.CommittedPrepares.Count);
		}
	}
}
