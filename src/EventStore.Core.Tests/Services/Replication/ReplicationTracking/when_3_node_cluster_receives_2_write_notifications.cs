using System;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.ReplicationTracking {
	[TestFixture]
	public class when_3_node_cluster_receives_2_write_notifications : with_clustered_replication_tracking_service {
		private long _logPosition = 4000;

		public override void When() {
			BecomeMaster();
			WriterCheckpoint.Write(_logPosition);
			WriterCheckpoint.Flush();
			Service.Handle(new ReplicationTrackingMessage.WriterCheckpointFlushed());
			Service.Handle(new ReplicationTrackingMessage.ReplicaWriteAck(Guid.NewGuid(), _logPosition));
			AssertEx.IsOrBecomesTrue(() => Service.IsCurrent());
		}

		[Test]
		public void replicated_to_should_be_sent() {
			AssertEx.IsOrBecomesTrue(() => 1 == ReplicatedTos.Count);
			Assert.True(ReplicatedTos.TryDequeue(out var msg));
			Assert.AreEqual(_logPosition, msg.LogPosition);
		}
		[Test]
		public void replication_checkpoint_should_advance() {
			Assert.AreEqual(_logPosition, ReplicationCheckpoint.Read());
			Assert.AreEqual(_logPosition, ReplicationCheckpoint.ReadNonFlushed());
		}
	}
}
