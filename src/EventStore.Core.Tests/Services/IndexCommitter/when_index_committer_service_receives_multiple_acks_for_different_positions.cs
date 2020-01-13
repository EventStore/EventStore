using System;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.IndexCommitter {
	[TestFixture]
	public class  when_index_committer_service_receives_multiple_acks_for_different_positions : with_index_committer_service {

		private readonly long _logPositionP1 = 1000;
		private readonly long _logPositionP2 = 2000;
		private readonly long _logPositionP3 = 3000;
		private readonly long _logPositionCommit1 = 3100;
		private readonly long _logPositionCommit2 = 3200;
		private readonly long _logPositionCommit3 = 3300;

		public override void Given() {
			
			AddPendingPrepare(_logPositionP1);
			AddPendingPrepare(_logPositionP2);
			AddPendingPrepare(_logPositionP3);

			Service.Handle(new StorageMessage.CommitAck(Guid.NewGuid(), _logPositionCommit1, _logPositionP1, 0, 0));
			Service.Handle(new StorageMessage.CommitAck(Guid.NewGuid(), _logPositionCommit2, _logPositionP2, 0, 0));
			Service.Handle(new StorageMessage.CommitAck(Guid.NewGuid(), _logPositionCommit3, _logPositionP3, 0, 0));
		}

		public override void When() {
			// Reach quorum for middle commit
			ReplicationCheckpoint.Write(_logPositionCommit2);
			ReplicationCheckpoint.Flush();
			Service.Handle(new ReplicationTrackingMessage.ReplicatedTo(_logPositionCommit2));
			
		}
		
		[Test]
		public void commit_replicated_message_should_have_been_published_for_first_two_events() {
			AssertEx.IsOrBecomesTrue(() => 2 == CommitReplicatedMgs.Count);
			Assert.AreEqual(_logPositionP1, CommitReplicatedMgs[0].TransactionPosition);
			Assert.AreEqual(_logPositionP2, CommitReplicatedMgs[1].TransactionPosition);
		}
		[Test]
		public void index_written_message_should_have_been_published_for_first_two_events() {
			AssertEx.IsOrBecomesTrue(() => 2 == IndexWrittenMgs.Count);
			Assert.AreEqual(_logPositionCommit1, IndexWrittenMgs[0].LogPosition);
			Assert.AreEqual(_logPositionCommit2, IndexWrittenMgs[1].LogPosition);
		}

		[Test]
		public void index_should_have_been_updated() {
			AssertEx.IsOrBecomesTrue(() => 2 == IndexCommitter.CommittedPrepares.Count);
			Assert.AreEqual(_logPositionP1, IndexCommitter.CommittedPrepares[0].LogPosition);
			Assert.AreEqual(_logPositionP2, IndexCommitter.CommittedPrepares[1].LogPosition);
		}
	}
}
