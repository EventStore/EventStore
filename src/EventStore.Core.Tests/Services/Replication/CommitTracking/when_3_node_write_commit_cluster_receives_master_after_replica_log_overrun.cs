using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Commit;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitTracking {
	[TestFixture]
	public class when_3_node_write_commit_cluster_receives_master_after_replica_log_overrun : with_clustered_commit_tracker_service {
		private long _logPosition = 4000;
		private long _overrunPosition = 5000;
		public override void TestFixtureSetUp() {
			Publisher.Subscribe(new AdHocHandler<CommitMessage.LogCommittedTo>(msg => LogCommittedTos.Add(msg)));
			Publisher.Subscribe(new AdHocHandler<CommitMessage.CommittedTo>(CommittedTos.Add));

			Service = new CommitTrackerService(Publisher, CommitLevel.ClusterWrite, ClusterSize);
			Service.Start();
			When();
		}
		public override void When() {
			BecomeMaster();
			var replicaId = Guid.NewGuid();
			Service.Handle(new CommitMessage.LogWrittenTo(_logPosition));
			Service.Handle(new CommitMessage.ReplicaLogWrittenTo(_overrunPosition, replicaId));
			AssertEx.IsOrBecomesTrue(() => Service.IsIdle());
			Service.Handle(new CommitMessage.LogWrittenTo(_overrunPosition));
			AssertEx.IsOrBecomesTrue(() => Service.IsIdle());
		}

		[Test]
		public void log_committed_to_should_be_sent() {
			Assert.AreEqual(2, LogCommittedTos.Count);
			var commit = LogCommittedTos[0];
			Assert.NotNull(commit);
			Assert.AreEqual(_logPosition, commit.LogPosition);
		    commit = LogCommittedTos[1];
			Assert.NotNull(commit);
			Assert.AreEqual(_overrunPosition, commit.LogPosition);
		}

		[Test]
		public void committed_to_should_be_sent() {
			Assert.AreEqual(2, CommittedTos.Count);
			var commit = LogCommittedTos[0];
			Assert.NotNull(commit);
			Assert.AreEqual(_logPosition, commit.LogPosition);
		    commit = LogCommittedTos[1];
			Assert.NotNull(commit);
			Assert.AreEqual(_overrunPosition, commit.LogPosition);
		}
	}
}
