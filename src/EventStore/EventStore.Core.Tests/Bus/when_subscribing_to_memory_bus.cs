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
using EventStore.Core.Bus;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Core.Tests.Helper;
using NUnit.Framework;

namespace EventStore.Core.Tests.Bus
{
    [TestFixture]
    public class when_subscribing_to_memory_bus
    {
        private InMemoryBus _bus;

        [SetUp]
        public void SetUp()
        {
            _bus = new InMemoryBus("test_bus", watchSlowMsg: false);
        }

        [TearDown]
        public void TearDown()
        {
            _bus = null;
        }

        [Test]
        public void null_as_handler_app_should_throw_arg_null_exception()
        {
            Assert.Throws<ArgumentNullException>(() => _bus.Subscribe<TestMessage>(null));
        }

        [Test]
        public void but_not_publishing_messages_noone_should_handle_any_messages()
        {
            var multiHandler = new TestMultiHandler();
            _bus.Subscribe<TestMessage>(multiHandler);
            _bus.Subscribe<TestMessage2>(multiHandler);
            _bus.Subscribe<TestMessage3>(multiHandler);

            Assert.That(multiHandler.HandledMessages.Count == 0);
        }

        [Test]
        public void one_handler_to_one_message_it_should_be_handled()
        {
            var handler = new TestHandler<TestMessage>();
            _bus.Subscribe<TestMessage>(handler);

            _bus.Publish(new TestMessage());

            Assert.That(handler.HandledMessages.ContainsSingle<TestMessage>());
        }

        [Test]
        public void one_handler_to_multiple_messages_they_all_should_be_handled()
        {
            var multiHandler = new TestMultiHandler();
            _bus.Subscribe<TestMessage>(multiHandler);
            _bus.Subscribe<TestMessage2>(multiHandler);
            _bus.Subscribe<TestMessage3>(multiHandler);

            _bus.Publish(new TestMessage());
            _bus.Publish(new TestMessage2());
            _bus.Publish(new TestMessage3());

            Assert.That(multiHandler.HandledMessages.ContainsSingle<TestMessage>() &&
                        multiHandler.HandledMessages.ContainsSingle<TestMessage2>() &&
                        multiHandler.HandledMessages.ContainsSingle<TestMessage3>());
        }

        [Test]
        public void one_handler_to_few_messages_then_only_subscribed_should_be_handled()
        {
            var multiHandler = new TestMultiHandler();
            _bus.Subscribe<TestMessage>(multiHandler);
            _bus.Subscribe<TestMessage3>(multiHandler);

            _bus.Publish(new TestMessage());
            _bus.Publish(new TestMessage2());
            _bus.Publish(new TestMessage3());

            Assert.That(multiHandler.HandledMessages.ContainsSingle<TestMessage>() &&
                        multiHandler.HandledMessages.ContainsNo<TestMessage2>() &&
                        multiHandler.HandledMessages.ContainsSingle<TestMessage3>());
        }

        [Test]
        public void multiple_handlers_to_one_message_then_each_handler_should_handle_message_once()
        {
            var handler1 = new TestHandler<TestMessage>();
            var handler2 = new TestHandler<TestMessage>();

            _bus.Subscribe<TestMessage>(handler1);
            _bus.Subscribe<TestMessage>(handler2);

            _bus.Publish(new TestMessage());

            Assert.That(handler1.HandledMessages.ContainsSingle<TestMessage>());
            Assert.That(handler2.HandledMessages.ContainsSingle<TestMessage>());
        }

        [Test]
        public void multiple_handlers_to_multiple_messages_then_each_handler_should_handle_subscribed_messages()
        {
            var handler1 = new TestMultiHandler();
            var handler2 = new TestMultiHandler();
            var handler3 = new TestMultiHandler();

            _bus.Subscribe<TestMessage>(handler1);
            _bus.Subscribe<TestMessage3>(handler1);

            _bus.Subscribe<TestMessage>(handler2);
            _bus.Subscribe<TestMessage2>(handler2);

            _bus.Subscribe<TestMessage2>(handler3);
            _bus.Subscribe<TestMessage3>(handler3);

            _bus.Publish(new TestMessage());
            _bus.Publish(new TestMessage2());
            _bus.Publish(new TestMessage3());

            Assert.That(handler1.HandledMessages.ContainsSingle<TestMessage>() &&
                        handler1.HandledMessages.ContainsSingle<TestMessage3>() &&

                        handler2.HandledMessages.ContainsSingle<TestMessage>() &&
                        handler2.HandledMessages.ContainsSingle<TestMessage2>() &&

                        handler3.HandledMessages.ContainsSingle<TestMessage2>() &&
                        handler3.HandledMessages.ContainsSingle<TestMessage3>() );
        }

        [Test]
        public void multiple_handlers_to_multiple_messages_then_each_handler_should_handle_only_subscribed_messages()
        {
            var handler1 = new TestMultiHandler();
            var handler2 = new TestMultiHandler();
            var handler3 = new TestMultiHandler();

            _bus.Subscribe<TestMessage>(handler1);
            _bus.Subscribe<TestMessage3>(handler1);

            _bus.Subscribe<TestMessage>(handler2);
            _bus.Subscribe<TestMessage2>(handler2);

            _bus.Subscribe<TestMessage2>(handler3);
            _bus.Subscribe<TestMessage3>(handler3);


            _bus.Publish(new TestMessage());
            _bus.Publish(new TestMessage2());
            _bus.Publish(new TestMessage3());


            Assert.That(handler1.HandledMessages.ContainsSingle<TestMessage>() &&
                        handler1.HandledMessages.ContainsNo<TestMessage2>() &&
                        handler1.HandledMessages.ContainsSingle<TestMessage3>() &&

                        handler2.HandledMessages.ContainsSingle<TestMessage>() &&
                        handler2.HandledMessages.ContainsSingle<TestMessage2>() &&
                        handler2.HandledMessages.ContainsNo<TestMessage3>() &&

                        handler3.HandledMessages.ContainsNo<TestMessage>() &&
                        handler3.HandledMessages.ContainsSingle<TestMessage2>() &&
                        handler3.HandledMessages.ContainsSingle<TestMessage3>() );
        }

        [Test]
        public void same_handler_to_same_message_few_times_then_message_should_be_handled_only_once()
        {
            var handler = new TestHandler<TestMessage>();
            _bus.Subscribe<TestMessage>(handler);
            _bus.Subscribe<TestMessage>(handler);
            _bus.Subscribe<TestMessage>(handler);

            _bus.Publish(new TestMessage());

            Assert.That(handler.HandledMessages.ContainsSingle<TestMessage>());
        }
    }
}