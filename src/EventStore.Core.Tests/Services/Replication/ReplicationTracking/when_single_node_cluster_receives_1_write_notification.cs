using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.ReplicationTracking {
	[TestFixture]
	public class when_single_node_cluster_receives_1_write_notification : with_clustered_commit_tracker_service {
		private long _logPosition = 4000;
		public override void TestFixtureSetUp() {
			ClusterSize = 1;
			base.TestFixtureSetUp();
		}
		public override void When() {
			BecomeMaster();
			WriterCheckpoint.Write(_logPosition);
			WriterCheckpoint.Flush();
			Service.Handle(new ReplicationTrackingMessage.WriterCheckpointFlushed());
			AssertEx.IsOrBecomesTrue(()=> Service.IsIdle());
		}

		[Test]
		public void replicated_to_should_be_sent() {
			Assert.AreEqual(1, ReplicatedTos.Count);
			var commit = ReplicatedTos[0];
			Assert.NotNull(commit);
			Assert.AreEqual(_logPosition, commit.LogPosition);
		}
		[Test]
		public void replication_checkpoint_should_advance() {
			Assert.AreEqual(_logPosition, ReplicationCheckpoint.Read());		
			Assert.AreEqual(_logPosition, ReplicationCheckpoint.ReadNonFlushed());		
		}	
	}
}
