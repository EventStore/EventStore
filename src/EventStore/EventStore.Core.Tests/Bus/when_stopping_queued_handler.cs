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
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Tests.Bus.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Bus
{
    [TestFixture]
    public class when_stopping_queued_handler : QueuedHandlerTestWithNoopConsumer
    {
        [Test]
        public void gracefully_should_not_throw()
        {
            Queue.Start();
            Assert.DoesNotThrow(() => Queue.Stop());
        }

        [Test]
        public void gracefully_and_queue_is_not_busy_should_not_take_much_time()
        {
            Queue.Start();
            
            var wait = new ManualResetEventSlim(false);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                Queue.Stop();
                wait.Set();
            });

            Assert.IsTrue(wait.Wait(1000), "Couldn't stop queue in time.");
        }

        [Test]
        public void second_time_should_not_throw()
        {
            Queue.Start();
            Queue.Stop();
            Assert.DoesNotThrow(() => Queue.Stop());
        }

        [Test]
        public void second_time_should_not_take_much_time()
        {
            Queue.Start();
            Queue.Stop();

            var wait = new ManualResetEventSlim(false);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                Queue.Stop();
                wait.Set();
            });

            Assert.IsTrue(wait.Wait(10), "Couldn't stop queue in time.");
        }

        [Test]
        public void while_queue_is_busy_should_crash_with_timeout()
        {
            var consumer = new WaitingConsumer(1);
            var busyQueue = new QueuedHandler(consumer, "busy_test_queue", watchSlowMsg: false, threadStopWaitTimeout: TimeSpan.FromMilliseconds(100));
            var waitHandle = new ManualResetEvent(false);
            var handledEvent = new ManualResetEvent(false);
            try
            {
                busyQueue.Start();
                busyQueue.Publish(new DeferredExecutionTestMessage(() =>
                {
                    handledEvent.Set();
                    waitHandle.WaitOne();
                }));

                handledEvent.WaitOne();
                Assert.Throws<TimeoutException>(() => busyQueue.Stop());
            }
            finally
            {
                waitHandle.Set();
                consumer.Wait();

                busyQueue.Stop();
                waitHandle.Dispose();
                handledEvent.Dispose();
                consumer.Dispose();
            }
        }
    }
}