using System;
using EventStore.Core.Data;
using EventStore.Core.Services.PersistentSubscription;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services
{
    [TestFixture]
    public class CheckpointingQueue_should
    {
        [Test]
        public void skip_events_in_sequence_until_gap()
        {
            int lastInSequence = 0;
            var queue = new CheckpointingQueue(x => lastInSequence = x);

            queue.Enqueue(new SequencedEvent(0, CreateEvent(10)));
            queue.Enqueue(new SequencedEvent(1, CreateEvent(11)));
            queue.Enqueue(new SequencedEvent(2, CreateEvent(12)));
            queue.Enqueue(new SequencedEvent(4, CreateEvent(13)));

            queue.MarkCheckpoint();

            Assert.AreEqual(11, lastInSequence);
        }

        private static ResolvedEvent CreateEvent(int number)
        {
            return new ResolvedEvent(
                    new EventRecord(
                        number, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "stream", 0, DateTime.MinValue, PrepareFlags.IsJson,
                        "type", new byte[0], new byte[0]));
        }
    }
}