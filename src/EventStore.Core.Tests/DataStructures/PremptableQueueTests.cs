using EventStore.Core.DataStructures;
using NUnit.Framework;

namespace EventStore.Core.Tests.DataStructures
{
    [TestFixture]
    public class PremptableQueueTests
    {
        [Test]
        public void enqueuing_and_dequeing_works_normally()
        {
            var queue = new PreemptableQueue<int>();
            queue.Enqueue(1);
            queue.Enqueue(2);
            queue.Enqueue(3);
            Assert.AreEqual(3, queue.Count);
            Assert.AreEqual(1, queue.Dequeue());
            Assert.AreEqual(2, queue.Dequeue());
            Assert.AreEqual(3, queue.Dequeue());
            Assert.AreEqual(0, queue.Count);
        }

        [Test]
        public void preempting_puts_messages_at_beginnng_of_queue()
        {
            var queue = new PreemptableQueue<int>();
            queue.Enqueue(1);
            queue.Enqueue(2);
            queue.Preempt(3);
            Assert.AreEqual(3, queue.Count);
            Assert.AreEqual(3, queue.Dequeue());
            Assert.AreEqual(1, queue.Dequeue());
            Assert.AreEqual(2, queue.Dequeue());
            Assert.AreEqual(0, queue.Count);
        }

        [Test]
        public void multi_preempting_is_also_fifo()
        {
            var queue = new PreemptableQueue<int>();
            queue.Enqueue(1);
            queue.Enqueue(2);
            queue.Preempt(3);
            queue.Preempt(4);
            Assert.AreEqual(4, queue.Count);
            Assert.AreEqual(3, queue.Dequeue());
            Assert.AreEqual(4,queue.Dequeue());
            Assert.AreEqual(1, queue.Dequeue());
            Assert.AreEqual(2, queue.Dequeue());
            Assert.AreEqual(0, queue.Count);
        }


    }
}