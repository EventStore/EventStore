using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.LeaderReplication {
	[TestFixture]
	public class when_replication_service_has_replica_disconnected : with_replication_service {
		
		public override void When() {
			ReplicaManager1.Stop();
			AssertEx.IsOrBecomesTrue(() => ReplicaManager1.IsClosed);

			//trigger main processing loop
			var writePos = DbConfig.WriterCheckpoint.Read();
			DbConfig.WriterCheckpoint.Write(writePos + 100);
			DbConfig.WriterCheckpoint.Flush();
		}
		
		[Test]
		public void vnode_disconnected_should_be_published() {
			AssertEx.IsOrBecomesTrue(() => ReplicaLostMessages.Count == 1, msg: "ReplicaLost msg not received");
			Assert.True(ReplicaLostMessages.TryDequeue(out var lost));

			Assert.AreEqual(ReplicaId, lost.SubscriptionId);
		}

		[Test]
		public void post_disconnect_replica_Log_written_to_should_not_be_published() {
			AssertEx.IsOrBecomesTrue(() => ReplicaLostMessages.Count == 1, msg: "ReplicaLost msg not received");
			Service.Handle(new ReplicationMessage.ReplicaLogPositionAck(ReplicaId, DbConfig.WriterCheckpoint.Read() + 200));
			
			Assert.True(ReplicaWriteAcks.Count == 0, $"Got unexpected ReplicaLogAck Message");
		}
	}
}
