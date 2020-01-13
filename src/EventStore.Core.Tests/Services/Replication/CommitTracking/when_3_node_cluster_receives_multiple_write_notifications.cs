using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitTracking {
	[TestFixture]
	public class when_3_node_cluster_receives_multiple_write_notifications : with_clustered_commit_tracker_service {
		private long _logPosition = 4000;
		private long _logPosition2 = 5000;
		private long _logPosition3 = 6000;

		public override void When() {
			BecomeMaster();
			var replicaId = Guid.NewGuid();
			Assert.Fail("Fix Test");
			//Service.Handle(new ReplicationTrackingMessage.WrittenTo(_logPosition));
			//Service.Handle(new ReplicationTrackingMessage.ReplicaWrittenTo(_logPosition, replicaId));
			//AssertEx.IsOrBecomesTrue(() => Service.IsIdle());
			//Service.Handle(new ReplicationTrackingMessage.ReplicaWrittenTo(_logPosition2, replicaId));
			//Service.Handle(new ReplicationTrackingMessage.WrittenTo(_logPosition2));
			//AssertEx.IsOrBecomesTrue(() => Service.IsIdle());
			//Service.Handle(new ReplicationTrackingMessage.WrittenTo(_logPosition3));
			//Service.Handle(new ReplicationTrackingMessage.ReplicaWrittenTo(_logPosition3, replicaId));
			//AssertEx.IsOrBecomesTrue(() => Service.IsIdle());
		}

		[Test]
		public void replicated_to_should_be_sent() {
			Assert.AreEqual(3, ReplicatedTos.Count);
			Assert.AreEqual(_logPosition, ReplicatedTos[0].LogPosition);
			Assert.AreEqual(_logPosition2, ReplicatedTos[1].LogPosition);
			Assert.AreEqual(_logPosition3, ReplicatedTos[2].LogPosition);
		}
	}
}
