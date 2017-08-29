using System.Linq;
using EventStore.Core.Messages;
using EventStore.Core.Services.TimerService;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CheckpointFilter
{
    [TestFixture]
    public class when_processing_multiple_event_committed_messages_for_the_same_positions : with_replication_checkpoint_filter
    {
        private TimerMessage.Schedule _scheduledMessage;

        public override void When()
        {
            _scheduledMessage = _publishConsumer.HandledMessages.OfType<TimerMessage.Schedule>().SingleOrDefault();
            _replicationChk.Write(-1);

            var msg1 = new StorageMessage.EventCommitted(1000, CreateDummyEventRecord(1000), false);
            var msg2 = new StorageMessage.EventCommitted(1000, CreateDummyEventRecord(1000), false);
            _filter.Handle(msg1);
            _filter.Handle(msg2);

            _replicationChk.Write(2000);
            _filter.Handle((ReplicationMessage.ReplicationCheckTick)_scheduledMessage.ReplyMessage);
        }

        [Test]
        public void should_publish_event_committed_message_for_both_messages()
        {
            var messages = _outputConsumer.HandledMessages.OfType<StorageMessage.EventCommitted>().ToList();
            Assert.AreEqual(2, messages.Count);
        }
    }
}