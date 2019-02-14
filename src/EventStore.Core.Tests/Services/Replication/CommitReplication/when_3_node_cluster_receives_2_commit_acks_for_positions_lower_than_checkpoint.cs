using System;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitReplication {
	[TestFixture]
	public class
		when_3_node_cluster_receives_2_commit_acks_for_positions_lower_than_checkpoint : with_index_committer_service {
		private Guid _correlationId = Guid.NewGuid();
		private long _logPosition = 2000;

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			_replicationPosition = 4000;
			base.TestFixtureSetUp();
		}

		public override void When() {
			BecomeMaster();
			_service.Handle(new StorageMessage.CommitAck(_correlationId, _logPosition, _logPosition, 0, 0, true));
			_service.Handle(new StorageMessage.CommitAck(_correlationId, _logPosition, _logPosition, 0, 0));
		}

		[Test]
		public void replication_checkpoint_should_not_have_been_updated() {
			Assert.AreEqual(_replicationPosition, _replicationCheckpoint.ReadNonFlushed());
		}

		[Test]
		public void commit_replicated_message_should_not_have_been_published() {
			Assert.AreEqual(0, _handledMessages.Count);
		}

		[Test]
		public void index_should_not_have_been_updated() {
			Assert.AreEqual(0, _indexCommitter.CommittedPrepares.Count);
		}
	}
}
