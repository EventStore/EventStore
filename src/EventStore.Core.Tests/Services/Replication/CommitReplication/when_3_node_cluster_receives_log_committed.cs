using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitReplication {
	[TestFixture]
	public class when_3_node_cluster_receives_log_committed : with_index_committer_service {
		private CountdownEvent _eventsReplicated = new CountdownEvent(1);

		private Guid _correlationId = Guid.NewGuid();
		private long _logPosition = 4000;

		public override void Given() { }
		public override void When() {
			Publisher.Subscribe(new AdHocHandler<StorageMessage.CommitIndexed>(m => _eventsReplicated.Signal()));
			BecomeMaster();
			AddPendingPrepare(_logPosition);
			Service.Handle(new StorageMessage.CommitAck(_correlationId, _logPosition, _logPosition, 0, 0));
			
			if (!_eventsReplicated.Wait(TimeSpan.FromSeconds(TimeoutSeconds))) {
				Assert.Fail("Timed out waiting for commit replicated messages to be published");
			}
		}

		[Test]
		public void replication_checkpoint_should_have_been_updated() {
			AssertEx.IsOrBecomesTrue(()=> _logPosition == ReplicationCheckpoint.ReadNonFlushed());
		}

		[Test]
		public void commit_replicated_message_should_have_been_published() {
			Assert.AreEqual(1, CommitReplicatedMgs.Count);
			Assert.AreEqual(_logPosition, CommitReplicatedMgs[0].TransactionPosition);
		}
		[Test]
		public void index_written_message_should_have_been_published() {
			Assert.AreEqual(1, IndexWrittenMgs.Count);
			Assert.AreEqual(_logPosition, IndexWrittenMgs[0].LogPosition);
		}
		[Test]
		public void index_should_have_been_updated() {
			Assert.AreEqual(1, IndexCommitter.CommittedPrepares.Count);
			Assert.AreEqual(_logPosition, IndexCommitter.CommittedPrepares[0].LogPosition);
		}
	}
}
