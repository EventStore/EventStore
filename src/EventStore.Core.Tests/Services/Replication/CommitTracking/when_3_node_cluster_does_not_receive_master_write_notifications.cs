using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitTracking {
	[TestFixture]
	public class when_3_node_cluster_does_not_receive_master_write_notifications : with_clustered_commit_tracker_service {
		private long _logPosition = 4000;

		public override void When() {
			BecomeMaster();
			var replicaId1 = Guid.NewGuid();
			var replicaId2 = Guid.NewGuid();
			Service.Handle(new CommitMessage.ReplicaLogWrittenTo(_logPosition, replicaId1));
			Service.Handle(new CommitMessage.ReplicaLogWrittenTo(_logPosition, replicaId2));
			AssertEx.IsOrBecomesTrue(() => Service.IsIdle());
		}

		[Test]
		public void log_committed_to_should_not_be_sent() {
			Assert.AreEqual(0, LogCommittedTos.Count);			
		}

		[Test]
		public void committed_to_should_not_be_sent() {
			Assert.AreEqual(0, CommittedTos.Count);
		}
	}
}
