using System;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.ReplicationTracking {
	[TestFixture]
	public class when_3_node_cluster_does_not_receive_master_write_notifications : with_clustered_replication_tracking_service {
		private long _logPosition = 4000;

		public override void When() {
			BecomeMaster();
			var replicaId1 = Guid.NewGuid();
			var replicaId2 = Guid.NewGuid();
			Service.Handle(new ReplicationTrackingMessage.ReplicaWriteAck(replicaId1,_logPosition));
			Service.Handle(new ReplicationTrackingMessage.ReplicaWriteAck(replicaId2,_logPosition));
			AssertEx.IsOrBecomesTrue(() => Service.IsCurrent());
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
