using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Commit;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitTracking {
	[TestFixture]
	public class when_3_node_write_commit_cluster_receives_2_writes : with_clustered_commit_tracker_service {
		private long _logPosition = 4000;
		public override void TestFixtureSetUp() {
			Publisher.Subscribe(new AdHocHandler<CommitMessage.LogCommittedTo>(msg =>  LogCommittedTos.Add(msg)));
			Publisher.Subscribe(new AdHocHandler<CommitMessage.CommittedTo>(CommittedTos.Add));
			
			Service = new CommitTrackerService(Publisher, CommitLevel.ClusterWrite, ClusterSize);
			Service.Start();
			When();
		}
		public override void When() {
			BecomeMaster();
			Service.Handle(new CommitMessage.LogWrittenTo(_logPosition));
			Service.Handle(new CommitMessage.ReplicaLogWrittenTo(_logPosition, Guid.NewGuid()));
			AssertEx.IsOrBecomesTrue(()=> Service.IsIdle());
		}

		[Test]
		public void log_committed_to_should_be_sent() {
			Assert.AreEqual(1, LogCommittedTos.Count);
		}

		[Test]
		public void committed_to_should_be_sent() {
			Assert.AreEqual(1, CommittedTos.Count);
		}
	}
}
