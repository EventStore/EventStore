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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Core.Tests.Bus.QueuedHandler.Helpers;
using EventStore.Core.Tests.Common;
using NUnit.Framework;

namespace EventStore.Core.Tests.Bus.QueuedHandler
{
    [TestFixture]
    public class when_publishing : QueuedHandlerTestWithWaitingConsumer
    {
        public override void SetUp()
        {
            base.SetUp();
            _queue.Start();
        }

        public override void TearDown()
        {
            _consumer.Dispose();
            _queue.Stop();
            base.TearDown();
        }

        [Test]
        public void null_message_should_throw()
        {
            Assert.Throws<ArgumentNullException>(() => _queue.Publish(null));
        }

        [Test]
        public void message_it_should_be_delivered_to_bus()
        {
            _consumer.SetWaitingCount(1);

            _queue.Publish(new TestMessage());

            _consumer.Wait();
            Assert.That(_consumer.HandledMessages.ContainsSingle<TestMessage>());
        }

        [Test]
        public void multiple_messages_they_should_be_delivered_to_bus()
        {
            _consumer.SetWaitingCount(2);

            _queue.Publish(new TestMessage());
            _queue.Publish(new TestMessage2());

            _consumer.Wait();

            Assert.That(_consumer.HandledMessages.ContainsSingle<TestMessage>() &&
                        _consumer.HandledMessages.ContainsSingle<TestMessage2>());
        }

        [Test]
        public void messages_from_different_threads_they_should_be_all_executed_in_same_one()
        {
            _consumer.SetWaitingCount(3);

            int msg1ThreadId = 0, msg2ThreadId = 1, msg3ThreadId = 2;

            var threads = new[] 
            {
                new Thread(() => _queue.Publish(new DeferredExecutionTestMessage(() =>{msg1ThreadId = Thread.CurrentThread.ManagedThreadId;}))),
                new Thread(() => _queue.Publish(new DeferredExecutionTestMessage(() =>{msg2ThreadId = Thread.CurrentThread.ManagedThreadId;}))),
                new Thread(() => _queue.Publish(new DeferredExecutionTestMessage(() =>{msg3ThreadId = Thread.CurrentThread.ManagedThreadId;})))
            };

            foreach (var thread in threads)
                thread.Start();

            bool executedOnTime = _consumer.Wait(500);

            if (!executedOnTime)
            {
                foreach (var thread in threads)
                    thread.Abort();
            }

            Assert.That(msg1ThreadId == msg2ThreadId && msg2ThreadId == msg3ThreadId);
        }

        [Test]
        public void messages_order_should_remain_the_same()
        {
            _consumer.SetWaitingCount(6);

            _queue.Publish(new TestMessageWithId(4));
            _queue.Publish(new TestMessageWithId(8));
            _queue.Publish(new TestMessageWithId(15));
            _queue.Publish(new TestMessageWithId(16));
            _queue.Publish(new TestMessageWithId(23));
            _queue.Publish(new TestMessageWithId(42));

            _consumer.Wait();

            var typedMessages = _consumer.HandledMessages.OfType<TestMessageWithId>().ToArray();
            Assert.That(typedMessages.Length == 6 &&
                        typedMessages[0].Id == 4 &&
                        typedMessages[1].Id == 8 &&
                        typedMessages[2].Id == 15 &&
                        typedMessages[3].Id == 16 &&
                        typedMessages[4].Id == 23 &&
                        typedMessages[5].Id == 42);
        }

        [Test]
        public void message_while_queue_has_hanged_it_should_not_be_executed()
        {
            _consumer.SetWaitingCount(2);
            var waitHandle = new ManualResetEvent(false);
            try
            {
                _queue.Publish(new DeferredExecutionTestMessage(() => waitHandle.WaitOne()));
                _queue.Publish(new TestMessage());

                Assert.That(_consumer.HandledMessages.ContainsNo<TestMessage>());
            }
            finally
            {
                waitHandle.Set();
                _consumer.Wait();
                waitHandle.Dispose();
            }
        }
    }
}
