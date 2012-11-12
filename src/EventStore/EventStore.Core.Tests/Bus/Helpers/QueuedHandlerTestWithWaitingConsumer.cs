using EventStore.Core.Bus;
using NUnit.Framework;

namespace EventStore.Core.Tests.Bus.Helpers
{
    public abstract class QueuedHandlerTestWithWaitingConsumer
    {
        protected QueuedHandler Queue;
        protected WaitingConsumer Consumer;

        [SetUp]
        public virtual void SetUp()
        {
            Consumer = new WaitingConsumer(0);
            Queue = new QueuedHandler(Consumer, "waiting_queue", watchSlowMsg: false, threadStopWaitTimeoutMs: 100);
        }

        [TearDown]
        public virtual void TearDown()
        {
            Queue.Stop();
            Queue = null;
            Consumer.Dispose();
            Consumer = null;
        }
    }
}