using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.ReplicationService {
	[TestFixture]
	public class when_replication_service_recieves_replica_log_position_ack : with_replication_service {
		private long _logPosition;
		
		public override void When() {			
			_logPosition = 4000;
			Service.Handle(new ReplicationMessage.ReplicaLogPositionAck(ReplicaId, _logPosition));
		}

		[Test]
		public void replica_Log_written_to_should_be_published() {
			AssertEx.IsOrBecomesTrue(() => ReplicaLogWrittenTos.Count == 1, msg:"ReplicaLogWrittenTo msg not recieved");
			var commit = ReplicaLogWrittenTos[0];
			Assert.NotNull(commit);
			Assert.AreEqual(ReplicaId, commit.ReplicaId);
			Assert.AreEqual(_logPosition, commit.LogPosition);
		}
	}
}
