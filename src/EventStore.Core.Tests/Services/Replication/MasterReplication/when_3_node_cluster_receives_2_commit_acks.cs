using System;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.MasterReplication
{
    [TestFixture]
    public class when_3_node_cluster_receives_2_commit_acks : with_master_replication_service
    {
        private long _logPosition = 5000;
        private Guid _correlationId = Guid.NewGuid();

        public override void When()
        {
            Service.Handle(new StorageMessage.CommitAck(_correlationId, _logPosition, _logPosition, 0, 0));
            Service.Handle(new StorageMessage.CommitAck(_correlationId, _logPosition, _logPosition, 0, 0));
        }

        [Test]
        public void replication_checkpoint_should_have_been_updated()
        {
            Assert.AreEqual(_logPosition, Db.Config.ReplicationCheckpoint.ReadNonFlushed());
        }
    }
}