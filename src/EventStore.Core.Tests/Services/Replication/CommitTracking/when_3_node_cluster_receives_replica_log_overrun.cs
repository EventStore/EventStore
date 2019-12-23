using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitTracking {
	[TestFixture]
	public class when_3_node_cluster_receives_replica_log_overrun : with_clustered_commit_tracker_service {
		private long _logPosition = 4000;

		public override void When() {
			BecomeMaster();
			var replicaId = Guid.NewGuid();
			Service.Handle(new CommitMessage.LogWrittenTo(_logPosition));
			Service.Handle(new CommitMessage.ReplicaLogWrittenTo(_logPosition + 100, replicaId));
			AssertEx.IsOrBecomesTrue(() => Service.IsIdle());
		}

		[Test]
		public void log_committed_to_should_be_sent() {
			Assert.AreEqual(1, LogCommittedTos.Count);
			var commit = LogCommittedTos[0];
			Assert.NotNull(commit);
			Assert.AreEqual(_logPosition, commit.LogPosition);
		}

		[Test]
		public void committed_to_should_not_be_sent() {
			Assert.AreEqual(0, CommittedTos.Count);
		}
	}
}
