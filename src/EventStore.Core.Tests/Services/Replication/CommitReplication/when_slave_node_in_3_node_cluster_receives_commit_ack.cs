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

		public override void When() {
			Publisher.Subscribe(new AdHocHandler<StorageMessage.CommitIndexed>(m => _eventsReplicated.Signal()));
			_expectedCommitReplicatedMessages = 1;
			BecomeSlave();
			AddPendingPrepare(_logPosition);
			Service.Handle(new StorageMessage.CommitAck(Guid.NewGuid(), _logPosition, _logPosition, 0, 0));
			Service.Handle(new CommitMessage.LogCommittedTo(_logPosition));

			if (!_eventsReplicated.Wait(TimeSpan.FromSeconds(TimeoutSeconds))) {
				Assert.Fail("Timed out waiting for commit replicated messages to be published");
			}
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
		public void index_written_message_should_not_have_been_published() {
			Assert.AreEqual(0, IndexWrittenMgs.Count);
		}

		[Test]
		public void index_should_have_been_updated() {
			Assert.AreEqual(1, IndexCommitter.CommittedPrepares.Count);
			Assert.AreEqual(_logPosition, IndexCommitter.CommittedPrepares[0].LogPosition);
		}
	}
}
