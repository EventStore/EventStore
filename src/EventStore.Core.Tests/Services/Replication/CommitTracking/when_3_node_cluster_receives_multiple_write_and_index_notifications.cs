using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitTracking {
	[TestFixture]
	public class when_3_node_cluster_receives_multiple_write_and_index_notifications : with_clustered_commit_tracker_service {
		private long _logPosition = 4000;
		private long _logPosition2 = 5000;
		private long _logPosition3 = 6000;

		public override void When() {
			BecomeMaster();
			var replicaId = Guid.NewGuid();
			Service.Handle(new CommitMessage.WrittenTo(_logPosition));
			Service.Handle(new CommitMessage.ReplicaWrittenTo(_logPosition, replicaId));
			Service.Handle(new CommitMessage.IndexedTo(_logPosition));
			AssertEx.IsOrBecomesTrue(() => Service.IsIdle());
			Service.Handle(new CommitMessage.ReplicaWrittenTo(_logPosition2, replicaId));
			Service.Handle(new CommitMessage.WrittenTo(_logPosition2));
			AssertEx.IsOrBecomesTrue(() => Service.IsIdle());
			Service.Handle(new CommitMessage.WrittenTo(_logPosition3));
			Service.Handle(new CommitMessage.ReplicaWrittenTo(_logPosition3, replicaId));
			Service.Handle(new CommitMessage.IndexedTo(_logPosition3));
			AssertEx.IsOrBecomesTrue(() => Service.IsIdle());
		}

		[Test]
		public void log_committed_to_should_be_sent() {
			Assert.AreEqual(3, LogCommittedTos.Count);		
			Assert.AreEqual(_logPosition, LogCommittedTos[0].LogPosition);
			Assert.AreEqual(_logPosition2, LogCommittedTos[1].LogPosition);
			Assert.AreEqual(_logPosition3, LogCommittedTos[2].LogPosition);
		}

		[Test]
		public void committed_to_should_not_be_sent() {
			Assert.AreEqual(2, CommittedTos.Count);
			Assert.AreEqual(_logPosition, CommittedTos[0].LogPosition);
			Assert.AreEqual(_logPosition3, CommittedTos[1].LogPosition);			
		}
	}
}
