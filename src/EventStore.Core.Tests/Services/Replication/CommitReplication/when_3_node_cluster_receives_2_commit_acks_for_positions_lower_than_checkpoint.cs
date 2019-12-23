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
			_replicationPosition = 4000;
			base.TestFixtureSetUp();
		}

		public override void When() {
			BecomeMaster();
			_service.Handle(new CommitMessage.LogCommittedTo(_logPosition));
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
