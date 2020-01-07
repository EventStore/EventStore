using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitReplication {
	[TestFixture]
	public class when_3_node_cluster_receives_multiple_acks_for_same_positions : with_index_committer_service {
		private CountdownEvent _eventsReplicated = new CountdownEvent(2);

		private Guid _correlationId1 = Guid.NewGuid();
		private Guid _correlationId2 = Guid.NewGuid();

		private long _logPosition = 1000;

		public override void When() {
			Publisher.Subscribe(new AdHocHandler<StorageMessage.CommitIndexed>(m => _eventsReplicated.Signal()));
			BecomeMaster();
			AddPendingPrepare(_logPosition);
			Service.Handle(new StorageMessage.CommitAck(_correlationId1, _logPosition, _logPosition, 0, 0, true));
			Service.Handle(new StorageMessage.CommitAck(_correlationId2, _logPosition, _logPosition, 0, 0, true));
			Service.Handle(new StorageMessage.CommitAck(_correlationId2, _logPosition, _logPosition, 0, 0));

			CommitTracker.Handle(new CommitMessage.WrittenTo(_logPosition));
			CommitTracker.Handle(new CommitMessage.ReplicaWrittenTo(_logPosition, Guid.NewGuid()));

			if (!_eventsReplicated.Wait(TimeSpan.FromSeconds(TimeoutSeconds))) {
				Assert.Fail("Timed out waiting for commit replicated messages to be published");
			}
		}

		[Test]
		public void replication_checkpoint_should_have_been_updated() {
			Assert.AreEqual(_logPosition, ReplicationCheckpoint.ReadNonFlushed());
		}

		[Test]
		public void commit_replicated_message_should_have_been_published_for_the_event() {
			Assert.AreEqual(2, CommitReplicatedMgs.Count);
		}
		[Test]
		public void index_written_message_should_have_been_published_for_the_event() {
			Assert.AreEqual(2, IndexWrittenMgs.Count);
			Assert.AreEqual(_logPosition, IndexWrittenMgs[0].LogPosition);
			Assert.AreEqual(_logPosition, IndexWrittenMgs[1].LogPosition);
		}

		[Test]
		public void index_should_have_been_updated() {
			Assert.AreEqual(1, IndexCommitter.CommittedPrepares.Count);
			Assert.AreEqual(_logPosition, IndexCommitter.CommittedPrepares[0].LogPosition);
		}
	}
}
