using System;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using NUnit.Framework;
using EventStore.Core.Tests.Helpers;

namespace EventStore.Core.Tests.Services.Replication.ReplicationService {
	[TestFixture]
	public class when_non_promotable_replica_sends_replica_log_position_ack : with_replication_service {
		private long _logPosition;
		
		public override void When() {
			_logPosition = 4000;
			Service.Handle(new ReplicationMessage.ReplicaLogPositionAck(ReadOnlyReplicaId, _logPosition));
		}

		[Test]
		public void replica_Log_written_to_should_not_be_published() {
			Assert.AreEqual(0, ReplicaWriteAcks.Count);			
		}
	}
}
