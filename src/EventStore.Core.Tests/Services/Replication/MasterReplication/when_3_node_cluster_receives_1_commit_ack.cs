using System;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.MasterReplication
{
    [TestFixture]
    public class when_3_node_cluster_receives_1_commit_ack : with_master_replication_service
    {
        private Guid _correlationId = Guid.NewGuid();
        private long _logPosition;

        public override void When()
        {
            _logPosition = 4000;
            Service.Handle(new StorageMessage.CommitAck(_correlationId, _logPosition, _logPosition, 0, 0));
        }

        [Test]
        public void replication_checkpoint_should_not_be_updated()
        {
            Assert.AreEqual(-1, Db.Config.ReplicationCheckpoint.ReadNonFlushed());
        }
    }
}