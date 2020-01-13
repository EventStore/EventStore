using System;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitReplication {
	[TestFixture]
	public class when_3_node_cluster_receives_master_log_write : with_index_committer_service {
		private long _logPosition = 4000;
		private Guid _correlationId = Guid.NewGuid();

		public override void Given() { }

		public override void When() {
			BecomeMaster();
			AddPendingPrepare(_logPosition, publishChaserMsgs: false);
			Service.Handle(new StorageMessage.CommitAck(_correlationId, _logPosition, _logPosition, 0, 0));	
			Assert.Fail("Fix Test");
			//CommitTracker.Handle(new ReplicationTrackingMessage.WrittenTo(_logPosition));
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
