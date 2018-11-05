using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitReplication
{
    [TestFixture]
    public class when_slave_node_in_3_node_cluster_receives_commit_ack : with_index_committer_service
    {
        private long _logPosition = 1000;
        private CountdownEvent _eventsReplicated = new CountdownEvent(1);

        public override void When()
        {
            _publisher.Subscribe(new AdHocHandler<StorageMessage.CommitReplicated>(m => _eventsReplicated.Signal()));
            _expectedCommitReplicatedMessages = 1;
            BecomeSlave();
            AddPendingPrepare(_logPosition);
            _service.Handle(new StorageMessage.CommitAck(Guid.NewGuid(), _logPosition, _logPosition, 0, 0));
            
            if(!_eventsReplicated.Wait(TimeSpan.FromSeconds(_timeoutSeconds)))
            {
                Assert.Fail("Timed out waiting for commit replicated messages to be published");
            }
        }

        [Test]
        public void replication_checkpoint_should_have_been_updated()
        {
            Assert.AreEqual(_logPosition, _replicationCheckpoint.ReadNonFlushed());
        }

        [Test]
        public void commit_replicated_message_should_have_been_published()
        {
            Assert.AreEqual(1, _handledMessages.Count);
            Assert.AreEqual(_logPosition, _handledMessages[0].TransactionPosition);
        }

        [Test]
        public void index_should_have_been_updated()
        {
            Assert.AreEqual(1, _indexCommitter.CommittedPrepares.Count);
            Assert.AreEqual(_logPosition, _indexCommitter.CommittedPrepares[0].LogPosition);
        }
    }
}