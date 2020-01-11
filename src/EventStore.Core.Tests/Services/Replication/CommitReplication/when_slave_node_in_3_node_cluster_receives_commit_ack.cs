using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitReplication {
	[TestFixture]
	public class when_slave_node_in_3_node_cluster_receives_commit_ack : with_index_committer_service {
		private long _logPosition = 1000;
		private CountdownEvent _eventsReplicated = new CountdownEvent(1);

		public override void Given() {
			_expectedCommitReplicatedMessages = 1;
			BecomeSlave();
			AddPendingPrepare(_logPosition, publishChaserMsgs: false);
			Service.Handle(new StorageMessage.CommitAck(Guid.NewGuid(), _logPosition, _logPosition, 0, 0));
		}

		public override void When() {
			Publisher.Publish(new CommitMessage.MasterReplicatedTo(_logPosition));
		}

		[Test]
		public void replication_checkpoint_should_have_been_updated() {
			AssertEx.IsOrBecomesTrue(() => _logPosition == ReplicationCheckpoint.ReadNonFlushed());
		}

		[Test]
		public void commit_replicated_message_should_have_been_published() {
			AssertEx.IsOrBecomesTrue(() => 1 == CommitReplicatedMgs.Count);
			Assert.AreEqual(_logPosition, CommitReplicatedMgs[0].TransactionPosition);
		}
		[Test]
		public void index_written_message_should_have_been_published() {
			AssertEx.IsOrBecomesTrue(() => 1 == IndexWrittenMgs.Count);
		}

		[Test]
		public void index_should_have_been_updated() {
			AssertEx.IsOrBecomesTrue(() => 1 == IndexCommitter.CommittedPrepares.Count);
			Assert.AreEqual(_logPosition, IndexCommitter.CommittedPrepares[0].LogPosition);
		}
	}
}
