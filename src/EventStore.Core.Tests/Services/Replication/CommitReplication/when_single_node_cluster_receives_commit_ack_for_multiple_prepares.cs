using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitReplication {
	[TestFixture]
	public class when_single_node_cluster_receives_commit_ack_for_multiple_prepares : with_index_committer_service {
		private CountdownEvent _eventsReplicated = new CountdownEvent(1);

		private long _transactionPosition = 4000;
		private long _logPosition1 = 4000;
		private long _logPosition2 = 5000;
		private long _logPosition3 = 6000;
		private long _commitPosition = 7000;

		public override void TestFixtureSetUp() {
			_commitCount = 1;
			base.TestFixtureSetUp();
		}

		public override void When() {
			_publisher.Subscribe(new AdHocHandler<StorageMessage.CommitReplicated>(m => _eventsReplicated.Signal()));

			BecomeMaster();
			AddPendingPrepares(_transactionPosition, new long[] {_logPosition1, _logPosition2, _logPosition3});
			AddPendingCommit(_transactionPosition, _commitPosition);
			_service.Handle(new StorageMessage.CommitAck(Guid.NewGuid(), _commitPosition, _transactionPosition, 0, 0,
				true));

			if (!_eventsReplicated.Wait(TimeSpan.FromSeconds(_timeoutSeconds))) {
				Assert.Fail("Timed out waiting for commit replicated messages to be published");
			}
		}

		[Test]
		public void replication_checkpoint_should_be_set_to_commit_position() {
			Assert.AreEqual(_commitPosition, _replicationCheckpoint.ReadNonFlushed());
		}

		[Test]
		public void commit_replicated_message_should_have_been_sent() {
			Assert.AreEqual(1, _handledMessages.Count);
			Assert.AreEqual(_commitPosition, _handledMessages[0].LogPosition);
		}

		[Test]
		public void index_should_have_been_updated_with_prepares() {
			Assert.AreEqual(3, _indexCommitter.CommittedPrepares.Count);
			Assert.AreEqual(_logPosition1, _indexCommitter.CommittedPrepares[0].LogPosition);
			Assert.AreEqual(_logPosition2, _indexCommitter.CommittedPrepares[1].LogPosition);
			Assert.AreEqual(_logPosition3, _indexCommitter.CommittedPrepares[2].LogPosition);
		}

		[Test]
		public void index_should_have_been_updated_with_commits() {
			Assert.AreEqual(1, _indexCommitter.CommittedCommits.Count);
			Assert.AreEqual(_commitPosition, _indexCommitter.CommittedCommits[0].LogPosition);
		}
	}
}
