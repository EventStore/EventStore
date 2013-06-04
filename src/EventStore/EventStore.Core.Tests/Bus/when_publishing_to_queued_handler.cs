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
using System.Linq;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Bus
{
    [TestFixture]
    public abstract class when_publishing_to_queued_handler : QueuedHandlerTestWithWaitingConsumer
    {
        protected when_publishing_to_queued_handler(Func<IHandle<Message>, string, TimeSpan, IQueuedHandler> queuedHandlerFactory)
                : base(queuedHandlerFactory)
        {
        }

        public override void SetUp()
        {
            base.SetUp();
            Queue.Start();
        }

        public override void TearDown()
        {
            Consumer.Dispose();
            Queue.Stop();
            base.TearDown();
        }

        [Test]
        public void null_message_should_throw()
        {
            Assert.Throws<ArgumentNullException>(() => Queue.Publish(null));
        }

        [Test]
        public void message_it_should_be_delivered_to_bus()
        {
            Consumer.SetWaitingCount(1);

            Queue.Publish(new TestMessage());

            Consumer.Wait();
            Assert.IsTrue(Consumer.HandledMessages.ContainsSingle<TestMessage>());
        }

        [Test]
        public void multiple_messages_they_should_be_delivered_to_bus()
        {
            Consumer.SetWaitingCount(2);

            Queue.Publish(new TestMessage());
            Queue.Publish(new TestMessage2());

            Consumer.Wait();

            Assert.IsTrue(Consumer.HandledMessages.ContainsSingle<TestMessage>());
            Assert.IsTrue(Consumer.HandledMessages.ContainsSingle<TestMessage2>());
        }

        [Test]
        public void messages_order_should_remain_the_same()
        {
            Consumer.SetWaitingCount(6);

            Queue.Publish(new TestMessageWithId(4));
            Queue.Publish(new TestMessageWithId(8));
            Queue.Publish(new TestMessageWithId(15));
            Queue.Publish(new TestMessageWithId(16));
            Queue.Publish(new TestMessageWithId(23));
            Queue.Publish(new TestMessageWithId(42));

            Consumer.Wait();

            var typedMessages = Consumer.HandledMessages.OfType<TestMessageWithId>().ToArray();
            Assert.AreEqual(6, typedMessages.Length);
            Assert.AreEqual(4, typedMessages[0].Id);
            Assert.AreEqual(8, typedMessages[1].Id);
            Assert.AreEqual(15, typedMessages[2].Id);
            Assert.AreEqual(16, typedMessages[3].Id);
            Assert.AreEqual(23, typedMessages[4].Id);
            Assert.AreEqual(42, typedMessages[5].Id);
        }
    }

    [TestFixture]
    public class when_publishing_to_queued_handler_mres : when_publishing_to_queued_handler
    {
        public when_publishing_to_queued_handler_mres()
            : base((consumer, name, timeout) => new QueuedHandlerMRES(consumer, name, false, null, timeout))
        {
        }
    }

    [TestFixture]
    public class when_publishing_to_queued_handler_autoreset : when_publishing_to_queued_handler
    {
        public when_publishing_to_queued_handler_autoreset()
            : base((consumer, name, timeout) => new QueuedHandlerAutoReset(consumer, name, false, null, timeout))
        {
        }
    }

    [TestFixture]
    public class when_publishing_to_queued_handler_sleep : when_publishing_to_queued_handler
    {
        public when_publishing_to_queued_handler_sleep()
            : base((consumer, name, timeout) => new QueuedHandlerSleep(consumer, name, false, null, timeout))
        {
        }
    }

    [TestFixture]
    public class when_publishing_to_queued_handler_pulse : when_publishing_to_queued_handler
    {
        public when_publishing_to_queued_handler_pulse()
            : base((consumer, name, timeout) => new QueuedHandlerPulse(consumer, name, false, null, timeout))
        {
        }
    }

    [TestFixture]
    public class when_publishing_to_queued_handler_threadpool : when_publishing_to_queued_handler
    {
        public when_publishing_to_queued_handler_threadpool()
            : base((consumer, name, timeout) => new QueuedHandlerThreadPool(consumer, name, false, null, timeout))
        {
        }
    }
}
