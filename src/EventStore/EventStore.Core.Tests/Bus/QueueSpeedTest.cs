// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//

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
