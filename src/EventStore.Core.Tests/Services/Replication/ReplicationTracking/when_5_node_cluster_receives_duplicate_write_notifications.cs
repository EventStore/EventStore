// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.ReplicationTracking {
	[TestFixture]
	public class when_5_node_cluster_receives_duplicate_write_notifications : with_clustered_replication_tracking_service {
		private long _logPosition = 4000;
		
		protected override int ClusterSize => 5;

		public override void When() {
			BecomeLeader();
			WriterCheckpoint.Write(_logPosition);
			WriterCheckpoint.Flush();
			Service.Handle(new ReplicationTrackingMessage.WriterCheckpointFlushed());
			var replicaId = Guid.NewGuid();
			Service.Handle(new ReplicationTrackingMessage.ReplicaWriteAck(replicaId, _logPosition));
			Service.Handle(new ReplicationTrackingMessage.ReplicaWriteAck(replicaId, _logPosition));
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
