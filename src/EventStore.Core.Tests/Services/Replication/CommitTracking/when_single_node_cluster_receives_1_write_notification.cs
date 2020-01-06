using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Commit;
using EventStore.Core.TransactionLog.Checkpoint;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitTracking {
	[TestFixture]
	public class when_single_node_cluster_receives_1_write_notification : with_clustered_commit_tracker_service {
		private long _logPosition = 4000;
		public override void TestFixtureSetUp() {
			Publisher.Subscribe(new AdHocHandler<CommitMessage.ReplicatedTo>(msg =>  LogCommittedTos.Add(msg)));
			Publisher.Subscribe(new AdHocHandler<CommitMessage.CommittedTo>(CommittedTos.Add));
			
			Service = new CommitTrackerService(Publisher, CommitLevel.MasterIndexed, 1, new InMemoryCheckpoint(0), new InMemoryCheckpoint(0));
			Service.Start();
			When();
		}
		public override void When() {
			BecomeMaster();
			Service.Handle(new CommitMessage.WrittenTo(_logPosition));
			AssertEx.IsOrBecomesTrue(()=> Service.IsIdle());
		}

		[Test]
		public void log_committed_to_should_be_sent() {
			Assert.AreEqual(1, LogCommittedTos.Count);
		}

		[Test]
		public void committed_to_should_not_be_sent() {
			Assert.AreEqual(0, CommittedTos.Count);
		}
	}
}
