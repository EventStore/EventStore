using System;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.ReplicationTracking {
	[TestFixture]
	public class when_3_node_cluster_receives_multiple_write_notifications : with_clustered_commit_tracker_service {
		private long _logPosition = 4000;
		private long _logPosition2 = 5000;
		private long _logPosition3 = 6000;

		public override void When() {
			BecomeMaster();
			var replicaId = Guid.NewGuid();
			WriterCheckpoint.Write(_logPosition);
			WriterCheckpoint.Flush();
			Service.Handle(new ReplicationTrackingMessage.WriterCheckpointFlushed());
			Service.Handle(new ReplicationTrackingMessage.ReplicaWriteAck(replicaId, _logPosition));
			AssertEx.IsOrBecomesTrue(() => Service.IsIdle());
			WriterCheckpoint.Write(_logPosition2);
			WriterCheckpoint.Flush();
			Service.Handle(new ReplicationTrackingMessage.ReplicaWriteAck(replicaId, _logPosition3));
			Service.Handle(new ReplicationTrackingMessage.WriterCheckpointFlushed());
			AssertEx.IsOrBecomesTrue(() => Service.IsIdle());
			WriterCheckpoint.Write(_logPosition3);
			WriterCheckpoint.Flush();
			Service.Handle(new ReplicationTrackingMessage.WriterCheckpointFlushed());
			Service.Handle(new ReplicationTrackingMessage.ReplicaWriteAck(replicaId, _logPosition3));
			AssertEx.IsOrBecomesTrue(() => Service.IsIdle());
		}

		[Test]
		public void replicated_to_should_be_sent() {
			Assert.AreEqual(3, ReplicatedTos.Count);
			Assert.AreEqual(_logPosition, ReplicatedTos[0].LogPosition);
			Assert.AreEqual(_logPosition2, ReplicatedTos[1].LogPosition);
			Assert.AreEqual(_logPosition3, ReplicatedTos[2].LogPosition);
		}
		[Test]
		public void replication_checkpoint_should_advance() {
			Assert.AreEqual(_logPosition3, ReplicationCheckpoint.Read());
			Assert.AreEqual(_logPosition3, ReplicationCheckpoint.ReadNonFlushed());
		}
	}
}
