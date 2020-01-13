using System;
using EventStore.Core.Messages;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.IndexCommitter {
	[TestFixture]
	public class
		when_index_committer_service_receives_multiple_acks_for_different_positions_out_of_order : with_index_committer_service {

		private readonly Guid _correlationId1 = Guid.NewGuid();
		private readonly Guid _correlationId2 = Guid.NewGuid();

		private readonly long _logPosition1 = 1000;
		private readonly long _logPosition2 = 2000;
		private readonly long _logPosition3 = 3000;
		private readonly long _logPosition4 = 4000;

		public override void Given() { }

		public override void When() {


			AddPendingPrepare(_logPosition2);
			AddPendingPrepare(_logPosition1);
			Service.Handle(new StorageMessage.CommitAck(_correlationId2, _logPosition4, _logPosition2, 0, 0));
			Service.Handle(new StorageMessage.CommitAck(_correlationId1, _logPosition3, _logPosition1, 0, 0));

				
			ReplicationCheckpoint.Write(_logPosition4);
			ReplicationCheckpoint.Flush();
			Service.Handle(new ReplicationTrackingMessage.ReplicatedTo(_logPosition4));
		}
		
		[Test]
		public void commit_replicated_message_should_have_been_published_for_both_events() {
			AssertEx.IsOrBecomesTrue(() => 2 == CommitReplicatedMgs.Count);
			Assert.AreEqual(_correlationId1, CommitReplicatedMgs[0].CorrelationId);
			Assert.AreEqual(_correlationId2, CommitReplicatedMgs[1].CorrelationId);
		}
		[Test]
		public void index_written_message_should_have_been_published_for_both_events() {
			AssertEx.IsOrBecomesTrue(() => 2 == IndexWrittenMgs.Count);
			Assert.AreEqual(_logPosition3, IndexWrittenMgs[0].LogPosition);
			Assert.AreEqual(_logPosition4, IndexWrittenMgs[1].LogPosition);
		}
		[Test]
		public void prepare_ack_message_should_have_been_published_for_both_events() {
			AssertEx.IsOrBecomesTrue(() => 2 == IndexCommitter.CommittedPrepares.Count);
			Assert.AreEqual(_logPosition1, IndexCommitter.CommittedPrepares[0].LogPosition);
			Assert.AreEqual(_logPosition2, IndexCommitter.CommittedPrepares[1].LogPosition);
		}
	}
}
