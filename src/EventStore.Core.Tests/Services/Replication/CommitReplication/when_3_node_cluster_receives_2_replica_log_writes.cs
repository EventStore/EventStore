using System;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitReplication {
	[TestFixture]
	public class when_3_node_cluster_receives_2_replica_log_writes : with_index_committer_service {
		private long _logPosition = 4000;

		public override void When() {
			BecomeMaster();
			AddPendingPrepare(_logPosition);
			_commitTracker.Handle(new CommitMessage.ReplicaLogWrittenTo(_logPosition, Guid.NewGuid()));
			_commitTracker.Handle(new CommitMessage.ReplicaLogWrittenTo(_logPosition, Guid.NewGuid()));
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
