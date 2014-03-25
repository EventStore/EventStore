using System;
using System.Diagnostics;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Bus.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Bus
{
    [TestFixture, Ignore]
    public class QueueSpeedTest
    {
        [Test, Category("LongRunning"), Explicit]
        [MightyMooseIgnore]
        public void autoreset_queued_handler_2_producers_50mln_messages()
        {
            QueuedHandlerAutoReset queue = null;
            SpeedTest(consumer =>
            {
                queue = new QueuedHandlerAutoReset(consumer, "Queue", false);
                queue.Start();
                return queue;
            }, 2, 50000000);
            queue.Stop();
        }

        [Test, Category("LongRunning"), Explicit]
        [MightyMooseIgnore]
        public void autoreset_queued_handler_10_producers_50mln_messages()
        {
            QueuedHandlerAutoReset queue = null;
            SpeedTest(consumer =>
            {
                queue = new QueuedHandlerAutoReset(consumer, "Queue", false);
                queue.Start();
                return queue;
            }, 10, 50000000);
            queue.Stop();
        }

        [Test, Category("LongRunning"), Explicit]
        [MightyMooseIgnore]
        public void sleep_queued_handler_2_producers_50mln_messages()
        {
            QueuedHandlerSleep queue = null;
            SpeedTest(consumer =>
            {
                queue = new QueuedHandlerSleep(consumer, "Queue", false);
                queue.Start();
                return queue;
            }, 2, 50000000);
            queue.Stop();
        }

        [Test, Category("LongRunning"), Explicit]
        [MightyMooseIgnore]
        public void sleep_queued_handler_10_producers_50mln_messages()
        {
            QueuedHandlerSleep queue = null;
            SpeedTest(consumer =>
            {
                queue = new QueuedHandlerSleep(consumer, "Queue", false);
                queue.Start();
                return queue;
            }, 10, 50000000);
            queue.Stop();
        }

        [Test, Category("LongRunning"), Explicit]
        [MightyMooseIgnore]
        public void pulse_queued_handler_2_producers_50mln_messages()
        {
            QueuedHandlerPulse queue = null;
            SpeedTest(consumer =>
            {
                queue = new QueuedHandlerPulse(consumer, "Queue", false);
                queue.Start();
                return queue;
            }, 2, 50000000);
            queue.Stop();
        }

        [Test, Category("LongRunning"), Explicit]
        [MightyMooseIgnore]
        public void pulse_queued_handler_10_producers_50mln_messages()
        {
            QueuedHandlerPulse queue = null;
            SpeedTest(consumer =>
            {
                queue = new QueuedHandlerPulse(consumer, "Queue", false);
                queue.Start();
                return queue;
            }, 10, 50000000);
            queue.Stop();
        }

        [Test, Category("LongRunning"), Explicit]
        [MightyMooseIgnore]
        public void mres_queued_handler_2_producers_50mln_messages()
        {
            QueuedHandlerMRES queue = null;
            SpeedTest(consumer =>
            {
                queue = new QueuedHandlerMRES(consumer, "Queue", false);
                queue.Start();
                return queue;
            }, 2, 50000000);
            queue.Stop();
        }

        [Test, Category("LongRunning"), Explicit]
        [MightyMooseIgnore]
        public void mres_queued_handler_10_producers_50mln_messages()
        {
            QueuedHandlerMRES queue = null;
            SpeedTest(consumer =>
            {
                queue = new QueuedHandlerMRES(consumer, "Queue", false);
                queue.Start();
                return queue;
            }, 10, 50000000);
            queue.Stop();
        }

        private void SpeedTest(Func<IHandle<Message>, IPublisher> queueFactory, int producingThreads, int messageCnt)
        {
            var queue = queueFactory(new NoopConsumer());
            var threads = new Thread[producingThreads];
            int msgCnt = messageCnt;
            var startEvent = new ManualResetEventSlim(false);
            var endEvent = new CountdownEvent(producingThreads);
            var msg = new SystemMessage.SystemStart();
            for (int i = 0; i < producingThreads; ++i)
            {
                threads[i] = new Thread(() =>
                {
                    startEvent.Wait();

                    while (Interlocked.Decrement(ref msgCnt) > 0)
                    {
                        queue.Publish(msg);
                    }

                    endEvent.Signal();

                }) { IsBackground = true, Name = "Producer #" + i};
                threads[i].Start();
            }

            Thread.Sleep(500);

            var sw = Stopwatch.StartNew();
            startEvent.Set();
            endEvent.Wait();
            sw.Stop();

            Console.WriteLine("Queue: {0},\nProducers: {1},\nTotal messages: {2},\nTotal time: {3},\nTicks per 1000 items: {4}",
                              queue.GetType().Name,
                              producingThreads,
                              messageCnt,
                              sw.Elapsed,
                              sw.Elapsed.Ticks/(messageCnt/1000));
        }
    }
}
