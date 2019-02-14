using System;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitReplication {
	[TestFixture]
	public class when_3_node_cluster_receives_1_commit_ack : with_index_committer_service {
		private long _logPosition = 4000;

		public override void When() {
			BecomeMaster();
			AddPendingPrepare(_logPosition);
			_service.Handle(new StorageMessage.CommitAck(Guid.NewGuid(), _logPosition, _logPosition, 0, 0, true));
		}

		[Test]
		public void replication_checkpoint_should_not_be_updated() {
			Assert.AreEqual(0, _replicationCheckpoint.ReadNonFlushed());
		}

		[Test]
		public void commit_replicated_message_should_not_be_sent() {
			Assert.AreEqual(0, _handledMessages.Count);
		}

		[Test]
		public void index_should_not_have_been_updated() {
			Assert.AreEqual(0, _indexCommitter.CommittedPrepares.Count);
		}
	}
}
