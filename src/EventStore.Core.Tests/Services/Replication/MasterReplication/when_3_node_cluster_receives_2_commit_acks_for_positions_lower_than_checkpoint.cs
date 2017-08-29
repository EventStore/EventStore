using System;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.MasterReplication
{
    [TestFixture]
    public class when_3_node_cluster_receives_2_commit_acks_for_positions_lower_than_checkpoint : with_master_replication_service
    {
        private long _checkpointPosition = 6000;
        private long _logPosition = 3000;
        private Guid _correlationId = Guid.NewGuid();

        public override void When()
        {
            Db.Config.ReplicationCheckpoint.Write(_checkpointPosition);
            Service.Handle(new StorageMessage.CommitAck(_correlationId, _logPosition, _logPosition, 0, 0));
            Service.Handle(new StorageMessage.CommitAck(_correlationId, _logPosition, _logPosition, 0, 0));
        }

        [Test]
        public void replication_checkpoint_should_remain_at_higher_value()
        {
            Assert.AreEqual(_checkpointPosition, Db.Config.ReplicationCheckpoint.ReadNonFlushed());
        }
    }
}