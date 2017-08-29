using System;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.MasterReplication
{
    [TestFixture]
    public class when_3_node_cluster_receives_multiple_acks_for_different_positions : with_master_replication_service
    {
        private Guid _correlationId1 = Guid.NewGuid();
        private Guid _correlationId2 = Guid.NewGuid();
        private Guid _correlationId3 = Guid.NewGuid();

        private long _logPosition1 = 1000;
        private long _logPosition2 = 2000;
        private long _logPosition3 = 3000;

        public override void When()
        {
            Service.Handle(new StorageMessage.CommitAck(_correlationId1, _logPosition1, _logPosition1, 0, 0));
            Service.Handle(new StorageMessage.CommitAck(_correlationId2, _logPosition2, _logPosition2, 0, 0));
            Service.Handle(new StorageMessage.CommitAck(_correlationId3, _logPosition3, _logPosition3, 0, 0));

            // Reach quorum for middle commit
            Service.Handle(new StorageMessage.CommitAck(_correlationId2, _logPosition2, _logPosition2, 0, 0));
        }

        [Test]
        public void replication_checkpoint_should_be_updated()
        {
            Assert.AreEqual(_logPosition2, Db.Config.ReplicationCheckpoint.ReadNonFlushed());
        }

        [Test]
        public void replication_checkpoint_should_update_if_next_commit_is_acked()
        {
            Service.Handle(new StorageMessage.CommitAck(_correlationId3, _logPosition3, _logPosition3, 0, 0));
            Assert.AreEqual(_logPosition3, Db.Config.ReplicationCheckpoint.ReadNonFlushed());
        }
    }
}