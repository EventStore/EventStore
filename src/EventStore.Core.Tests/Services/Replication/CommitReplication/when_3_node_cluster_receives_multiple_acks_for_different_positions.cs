using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitReplication {
	[TestFixture]
	public class when_3_node_cluster_receives_multiple_acks_for_different_positions : with_index_committer_service {
		private CountdownEvent _eventsReplicated = new CountdownEvent(2);

		private Guid _correlationId1 = Guid.NewGuid();
		private Guid _correlationId2 = Guid.NewGuid();
		private Guid _correlationId3 = Guid.NewGuid();

		private long _logPosition1 = 1000;
		private long _logPosition2 = 2000;
		private long _logPosition3 = 3000;

		public override void When() {
			Publisher.Subscribe(new AdHocHandler<StorageMessage.CommitReplicated>(m => _eventsReplicated.Signal()));
			BecomeMaster();
			AddPendingPrepare(_logPosition1);
			AddPendingPrepare(_logPosition2);
			AddPendingPrepare(_logPosition3);
			Service.Handle(new StorageMessage.CommitAck(_correlationId1, _logPosition1, _logPosition1, 0, 0, true));
			Service.Handle(new StorageMessage.CommitAck(_correlationId2, _logPosition2, _logPosition2, 0, 0, true));
			Service.Handle(new StorageMessage.CommitAck(_correlationId3, _logPosition3, _logPosition3, 0, 0, true));

			// Reach quorum for middle commit
			Service.Handle(new CommitMessage.LogCommittedTo(_logPosition2));

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
			Assert.AreEqual(_logPosition1, CommitReplicatedMgs[0].TransactionPosition);
			Assert.AreEqual(_logPosition2, CommitReplicatedMgs[1].TransactionPosition);
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
