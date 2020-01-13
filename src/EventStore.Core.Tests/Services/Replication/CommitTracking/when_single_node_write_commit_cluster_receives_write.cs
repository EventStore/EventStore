using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Replication;
using EventStore.Core.TransactionLog.Checkpoint;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitTracking {
	[TestFixture]
	public class when_single_node_write_commit_cluster_receives_write : with_clustered_commit_tracker_service {
		private long _logPosition = 4000;
		public override void TestFixtureSetUp() {
			Publisher.Subscribe(new AdHocHandler<ReplicationTrackingMessage.ReplicatedTo>(msg => ReplicatedTos.Add(msg)));

			Service = new ReplicationTrackingService(Publisher, 1, new InMemoryCheckpoint(0), new InMemoryCheckpoint(0));
			Service.Start();
			When();
		}
		public override void When() {
			BecomeMaster();
			Assert.Fail("Fix Test");
			//Service.Handle(new ReplicationTrackingMessage.WrittenTo(_logPosition));
			AssertEx.IsOrBecomesTrue(() => Service.IsIdle());
		}

		[Test]
		public void replicated_to_should_be_sent() {
			Assert.AreEqual(1, ReplicatedTos.Count);
		}
	}
}
