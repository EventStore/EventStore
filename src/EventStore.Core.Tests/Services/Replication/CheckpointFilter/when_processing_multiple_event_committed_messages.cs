using System.Linq;
using EventStore.Core.Messages;
using EventStore.Core.Services.TimerService;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CheckpointFilter
{
    [TestFixture]
    public class when_processing_multiple_event_committed_messages : with_replication_checkpoint_filter
    {
        private TimerMessage.Schedule _scheduledMessage;

        public override void When()
        {
            _scheduledMessage = _publishConsumer.HandledMessages.OfType<TimerMessage.Schedule>().SingleOrDefault();
            _replicationChk.Write(-1);

            var msg1 = new StorageMessage.EventCommitted(1000, CreateDummyEventRecord(1000), false);
            var msg2 = new StorageMessage.EventCommitted(2000, CreateDummyEventRecord(2000), false);
            var msg3 = new StorageMessage.EventCommitted(3000, CreateDummyEventRecord(3000), false);
            _filter.Handle(msg1);
            _filter.Handle(msg2);
            _filter.Handle(msg3);

            _replicationChk.Write(2000);
            _filter.Handle((ReplicationMessage.ReplicationCheckTick)_scheduledMessage.ReplyMessage);
        }

        [Test]
        public void should_publish_event_committed_message_on_tick_for_first_two_messages()
        {
            var messages = _outputConsumer.HandledMessages.OfType<StorageMessage.EventCommitted>().ToList();
            Assert.AreEqual(2, messages.Count);
            Assert.IsTrue(messages.Any(x=> ((StorageMessage.EventCommitted)x).CommitPosition == 1000));
            Assert.IsTrue(messages.Any(x=> ((StorageMessage.EventCommitted)x).CommitPosition == 2000));
        }

        [Test]
        public void should_publish_third_event_on_tick_after_checkpoint_increases()
        {
            _outputConsumer.HandledMessages.Clear();
            _replicationChk.Write(3000);
            _filter.Handle((ReplicationMessage.ReplicationCheckTick)_scheduledMessage.ReplyMessage);
            Assert.AreEqual(1, _outputConsumer.HandledMessages.OfType<StorageMessage.EventCommitted>().Count());
        }
    }
}