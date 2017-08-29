using System.Linq;
using EventStore.Core.Messages;
using EventStore.Core.Services.TimerService;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CheckpointFilter
{
    [TestFixture]
    public class when_processing_event_committed_and_commit_position_is_greater_than_checkpoint : with_replication_checkpoint_filter
    {
        private TimerMessage.Schedule _scheduledMessage;

        public override void When()
        {
            _replicationChk.Write(2000);
            var msg = new StorageMessage.EventCommitted(3000, CreateDummyEventRecord(3000), false);
            _scheduledMessage = _publishConsumer.HandledMessages.OfType<TimerMessage.Schedule>().SingleOrDefault();

            _filter.Handle(msg);
        }

        [Test]
        public void should_not_publish_event_committed_message()
        {
            Assert.AreEqual(0, _outputConsumer.HandledMessages.OfType<StorageMessage.EventCommitted>().Count());
        }

        [Test]
        public void should_schedule_a_replication_checkpoint_check_message()
        {
            Assert.IsNotNull(_scheduledMessage);
            Assert.IsInstanceOf<ReplicationMessage.ReplicationCheckTick>(_scheduledMessage.ReplyMessage);
        }

        [Test]
        public void should_publish_event_committed_message_on_tick_after_checkpoint_increases()
        {
            _replicationChk.Write(4000);
            _filter.Handle((ReplicationMessage.ReplicationCheckTick)_scheduledMessage.ReplyMessage);
            Assert.AreEqual(1, _outputConsumer.HandledMessages.OfType<StorageMessage.EventCommitted>().Count());
        }
    }

}