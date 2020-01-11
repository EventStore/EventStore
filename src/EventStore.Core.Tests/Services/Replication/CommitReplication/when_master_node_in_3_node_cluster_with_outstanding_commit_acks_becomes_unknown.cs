using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitReplication {
	[TestFixture]
	public class
		when_master_node_in_3_node_cluster_with_outstanding_commit_acks_becomes_unknown : with_index_committer_service {

		private long _logPosition = 1000;

		public override void Given() {
			BecomeMaster();
			AddPendingPrepare(_logPosition, publishChaserMsgs: false);
			Service.Handle(new StorageMessage.CommitAck(Guid.NewGuid(), _logPosition, _logPosition, 0, 0));
			BecomeUnknown();
		}

		public override void When() {
			Publisher.Publish(new CommitMessage.MasterReplicatedTo(_logPosition));
		}

		[Test]
		public void replication_checkpoint_should_have_been_updated() {
			Assert.AreEqual(_logPosition, ReplicationCheckpoint.ReadNonFlushed());
		}

		[Test]
		public void commit_replicated_message_should_have_been_published() {
			Assert.AreEqual(1, CommitReplicatedMgs.Count);
			Assert.AreEqual(_logPosition, CommitReplicatedMgs[0].TransactionPosition);
		}
		[Test]
		public void index_written_message_should_be_published() {
			Assert.AreEqual(1, IndexWrittenMgs.Count);
		}

		[Test]
		public void index_should_have_been_updated() {
			Assert.AreEqual(1, IndexCommitter.CommittedPrepares.Count);
			Assert.AreEqual(_logPosition, IndexCommitter.CommittedPrepares[0].LogPosition);
		}
	}
}
