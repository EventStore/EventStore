using System.Linq;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CheckpointFilter
{
    [TestFixture]
    public class when_processing_event_committed_and_commit_position_is_less_than_replication : with_replication_checkpoint_filter
    {
        public override void When()
        {
            _replicationChk.Write(2000);
            var msg = new StorageMessage.EventCommitted(1000, CreateDummyEventRecord(1000), false);

            _filter.Handle(msg);
        }

        [Test]
        public void should_publish_event_committed_message()
        {
            Assert.AreEqual(1, _outputConsumer.HandledMessages.OfType<StorageMessage.EventCommitted>().Count());
        }
    }
}