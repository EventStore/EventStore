using System;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitReplication {
	[TestFixture]
	public class
		when_3_node_cluster_receives_log_committed_for_position_lower_than_checkpoint : with_index_committer_service {
		private Guid _correlationId = Guid.NewGuid();
		private long _logPosition = 2000;

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			ReplicationPosition = 4000;
			base.TestFixtureSetUp();
		}

		public override void When() {
			BecomeMaster();
			Service.Handle(new StorageMessage.CommitAck(_correlationId, _logPosition, _logPosition, 0, 0));
			Service.Handle(new CommitMessage.ReplicatedTo(_logPosition - 1));
		}

		[Test]
		public void replication_checkpoint_should_not_have_been_updated() {
			Assert.AreEqual(ReplicationPosition, ReplicationCheckpoint.ReadNonFlushed());
		}

		[Test]
		public void commit_replicated_message_should_not_have_been_published() {
			Assert.AreEqual(0, CommitReplicatedMgs.Count);
		}
		[Test]
		public void index_written_message_should_have_not_been_published() {
			Assert.AreEqual(0, IndexWrittenMgs.Count);
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
