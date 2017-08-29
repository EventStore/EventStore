using System;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Services.Replication;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CheckpointFilter
{
     public abstract class with_replication_checkpoint_filter
    {
        protected ReplicationCheckpointFilter _filter;
        protected InMemoryBus _outputBus;
        protected InMemoryBus _publisher;
        protected ICheckpoint _replicationChk;

        protected TestHandler<Message> _outputConsumer;
        protected TestHandler<Message> _publishConsumer;

        [OneTimeSetUp]
        public void SetUp()
        {
            _outputBus = new InMemoryBus("OutputBus");
            _publisher = new InMemoryBus("Publisher");
            _replicationChk = new InMemoryCheckpoint();

            _outputConsumer = new TestHandler<Message>();
            _outputBus.Subscribe(_outputConsumer);

            _publishConsumer = new TestHandler<Message>();
            _publisher.Subscribe(_publishConsumer);

            _filter = new ReplicationCheckpointFilter(_outputBus, _publisher, _replicationChk);

            When();
        }

        public abstract void When();

        protected EventRecord CreateDummyEventRecord(long logPosition)
        {
            return new EventRecord(1, logPosition, Guid.NewGuid(), Guid.NewGuid(), logPosition, 0, "test-stream",
                                   0, DateTime.Now, PrepareFlags.Data, "testEvent", new byte[0], new byte[0]);
        }
    }
}