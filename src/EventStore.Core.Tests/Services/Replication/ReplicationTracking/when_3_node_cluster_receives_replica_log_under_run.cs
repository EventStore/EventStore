using System;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.ReplicationTracking {
	[TestFixture]
	public class when_3_node_cluster_receives_replica_log_under_run : with_clustered_commit_tracker_service {
		private long _logPosition = 4000;
		private long _underRunPosition = 3000;

		public override void When() {
			BecomeMaster();
			var replicaId = Guid.NewGuid();
			WriterCheckpoint.Write(_logPosition);
			WriterCheckpoint.Flush();
			Service.Handle(new ReplicationTrackingMessage.WriterCheckpointFlushed());
			Service.Handle(new ReplicationTrackingMessage.ReplicaWriteAck( replicaId, _underRunPosition));
			AssertEx.IsOrBecomesTrue(() => Service.IsIdle());
		}

		[Test]
		public void replicated_to_should_be_sent() {
			Assert.AreEqual(1, ReplicatedTos.Count);
			var commit = ReplicatedTos[0];
			Assert.NotNull(commit);
			Assert.AreEqual(_underRunPosition, commit.LogPosition);
		}	
		[Test]
		public void replication_checkpoint_should_advance() {
			Assert.AreEqual(_underRunPosition, ReplicationCheckpoint.Read());		
			Assert.AreEqual(_underRunPosition, ReplicationCheckpoint.ReadNonFlushed());		
		}	
	}
}
