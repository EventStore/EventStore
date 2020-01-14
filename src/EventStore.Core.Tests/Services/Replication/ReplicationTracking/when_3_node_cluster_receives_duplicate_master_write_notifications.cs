using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.ReplicationTracking {
	[TestFixture]
	public class when_3_node_cluster_receives_duplicate_master_write_notifications : with_clustered_commit_tracker_service {
		private long _logPosition = 4000;

		public override void When() {
			BecomeMaster();
			WriterCheckpoint.Write(_logPosition);
			WriterCheckpoint.Flush();
			Service.Handle(new ReplicationTrackingMessage.WriterCheckpointFlushed());			
			Service.Handle(new ReplicationTrackingMessage.WriterCheckpointFlushed());	
			AssertEx.IsOrBecomesTrue(() => Service.IsIdle());
		}

		[Test]
		public void replicated_to_should_not_be_sent() {
			Assert.AreEqual(0, ReplicatedTos.Count);		
		}
		[Test]
		public void replication_checkpoint_should_not_advance() {
			Assert.AreEqual(0, ReplicationCheckpoint.Read());		
			Assert.AreEqual(0, ReplicationCheckpoint.ReadNonFlushed());		
		}
	}
}
