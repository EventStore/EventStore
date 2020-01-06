using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Commit;
using EventStore.Core.TransactionLog.Checkpoint;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitTracking {
	[TestFixture]
	public class when_3_node_write_commit_cluster_receives_replica_log_overrun : with_clustered_commit_tracker_service {
		private long _logPosition = 4000;
		public override void TestFixtureSetUp() {
			Publisher.Subscribe(new AdHocHandler<CommitMessage.ReplicatedTo>(msg => LogCommittedTos.Add(msg)));
			Publisher.Subscribe(new AdHocHandler<CommitMessage.CommittedTo>(CommittedTos.Add));

			Service = new CommitTrackerService(Publisher, CommitLevel.ClusterWrite, ClusterSize, new InMemoryCheckpoint(0), new InMemoryCheckpoint(0));
			Service.Start();
			When();
		}
		public override void When() {
			BecomeMaster();
			var replicaId = Guid.NewGuid();
			Service.Handle(new CommitMessage.WrittenTo(_logPosition));
			Service.Handle(new CommitMessage.ReplicaWrittenTo(_logPosition + 100, replicaId));
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
		public void committed_to_should_be_sent() {
			Assert.AreEqual(1, CommittedTos.Count);
			var commit = LogCommittedTos[0];
			Assert.NotNull(commit);
			Assert.AreEqual(_logPosition, commit.LogPosition);
		}
	}
}
