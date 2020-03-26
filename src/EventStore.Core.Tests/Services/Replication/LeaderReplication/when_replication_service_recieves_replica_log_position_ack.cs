using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.LeaderReplication {
	[TestFixture]
	public class when_replication_service_receives_replica_log_position_ack : with_replication_service {
		private long _logPosition;
		
		public override void When() {			
			_logPosition = 4000;
			Service.Handle(new ReplicationMessage.ReplicaLogPositionAck(ReplicaId, _logPosition));
		}

		[Test]
		public void replica_Log_written_to_should_be_published() {
			AssertEx.IsOrBecomesTrue(() => ReplicaWriteAcks.Count == 1, msg:"ReplicaLogWrittenTo msg not received");
			Assert.True(ReplicaWriteAcks.TryDequeue(out var commit));

			Assert.AreEqual(ReplicaId, commit.SubscriptionId);
			Assert.AreEqual(_logPosition, commit.ReplicationLogPosition);
		}
	}
}
