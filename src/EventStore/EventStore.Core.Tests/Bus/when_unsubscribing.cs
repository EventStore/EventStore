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
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Core.Tests.Common;
using NUnit.Framework;

namespace EventStore.Core.Tests.Bus
{
    [TestFixture]
    public class when_unsubscribing : BusTestBase
    {
        [Test]
        public void null_as_handler_app_should_throw()
        {
            Assert.Throws<ArgumentNullException>(() => Bus.Unsubscribe<TestMessage>(null));
        }

        [Test]
        public void not_subscribed_handler_app_doesnt_throw()
        {
            var handler = new TestHandler();
            Assert.DoesNotThrow(() => Bus.Unsubscribe<TestMessage>(handler));
        }

        [Test]
        public void same_handler_from_same_message_multiple_times_app_doesnt_throw()
        {
            var handler = new TestHandler();
            Assert.DoesNotThrow(() =>
                                    {
                                        Bus.Unsubscribe<TestMessage>(handler);
                                        Bus.Unsubscribe<TestMessage>(handler);
                                        Bus.Unsubscribe<TestMessage>(handler);
                                    });
        }

        [Test]
        public void multihandler_from_single_message_app_doesnt_throw()
        {
            var handler = new MultipleMessagesTestHandler();
            Bus.Subscribe<TestMessage>(handler);
            Bus.Subscribe<TestMessage2>(handler);
            Bus.Subscribe<TestMessage3>(handler);

            Assert.DoesNotThrow(() => Bus.Unsubscribe<TestMessage>(handler));
        }

        [Test]
        public void handler_from_message_it_should_not_handle_this_message_anymore()
        {
            var handler = new TestHandler();
            Bus.Subscribe<TestMessage>(handler);
            
            Bus.Unsubscribe<TestMessage>(handler);
            Bus.Publish(new TestMessage());

            Assert.That(handler.HandledMessages.IsEmpty());
        }

        [Test]
        public void handler_from_multiple_messages_they_all_should_not_be_handled_anymore()
        {
            var handler = new MultipleMessagesTestHandler();
            Bus.Subscribe<TestMessage>(handler);
            Bus.Subscribe<TestMessage2>(handler);
            Bus.Subscribe<TestMessage3>(handler);

            Bus.Unsubscribe<TestMessage>(handler);
            Bus.Unsubscribe<TestMessage2>(handler);
            Bus.Unsubscribe<TestMessage3>(handler);

            Bus.Publish(new TestMessage());
            Bus.Publish(new TestMessage2());
            Bus.Publish(new TestMessage3());

            Assert.That(handler.HandledMessages.ContainsNo<TestMessage>() &&
                        handler.HandledMessages.ContainsNo<TestMessage2>() &&
                        handler.HandledMessages.ContainsNo<TestMessage3>());
        }

        [Test]
        public void handler_from_message_it_should_not_handle_this_message_anymore_and_still_handle_other_messages()
        {
            var handler = new MultipleMessagesTestHandler();
            Bus.Subscribe<TestMessage>(handler);
            Bus.Subscribe<TestMessage2>(handler);
            Bus.Subscribe<TestMessage3>(handler);

            Bus.Unsubscribe<TestMessage>(handler);

            Bus.Publish(new TestMessage());
            Bus.Publish(new TestMessage2());
            Bus.Publish(new TestMessage3());

            Assert.That(handler.HandledMessages.ContainsNo<TestMessage>() &&
                        handler.HandledMessages.ContainsSingle<TestMessage2>() &&
                        handler.HandledMessages.ContainsSingle<TestMessage3>());
        }

        [Test]
        public void one_handler_and_leaving_others_subscribed_only_others_should_handle_message()
        {
            var handler1 = new TestHandler();
            var handler2 = new TestHandler();
            var handler3 = new TestHandler();

            Bus.Subscribe<TestMessage>(handler1);
            Bus.Subscribe<TestMessage>(handler2);
            Bus.Subscribe<TestMessage>(handler3);

            Bus.Unsubscribe(handler1);
            Bus.Publish(new TestMessage());

            Assert.That(handler1.HandledMessages.ContainsNo<TestMessage>() &&
                        handler2.HandledMessages.ContainsSingle<TestMessage>() &&
                        handler3.HandledMessages.ContainsSingle<TestMessage>());

        }

        [Test]
        public void all_handlers_from_message_noone_should_handle_message()
        {
            var handler1 = new TestHandler();
            var handler2 = new TestHandler();
            var handler3 = new TestHandler();

            Bus.Subscribe<TestMessage>(handler1);
            Bus.Subscribe<TestMessage>(handler2);
            Bus.Subscribe<TestMessage>(handler3);

            Bus.Unsubscribe(handler1);
            Bus.Unsubscribe(handler2);
            Bus.Unsubscribe(handler3);
            Bus.Publish(new TestMessage());

            Assert.That(handler1.HandledMessages.ContainsNo<TestMessage>() &&
                        handler2.HandledMessages.ContainsNo<TestMessage>() &&
                        handler3.HandledMessages.ContainsNo<TestMessage>());
        }

        [Test]
        public void handlers_after_publishing_message_all_is_still_done_correctly()
        {
            var handler1 = new TestHandler();
            var handler2 = new TestHandler();
            var handler3 = new TestHandler();

            Bus.Subscribe<TestMessage>(handler1);
            Bus.Subscribe<TestMessage>(handler2);
            Bus.Subscribe<TestMessage>(handler3);

            Bus.Publish(new TestMessage());
            handler1.HandledMessages.Clear();
            handler2.HandledMessages.Clear();
            handler3.HandledMessages.Clear();

            //just to ensure
            Assert.That(handler1.HandledMessages.ContainsNo<TestMessage>() &&
                        handler2.HandledMessages.ContainsNo<TestMessage>() &&
                        handler3.HandledMessages.ContainsNo<TestMessage>());

            Bus.Unsubscribe(handler1);
            Bus.Unsubscribe(handler2);
            Bus.Unsubscribe(handler3);
            Bus.Publish(new TestMessage());

            Assert.That(handler1.HandledMessages.ContainsNo<TestMessage>() &&
                        handler2.HandledMessages.ContainsNo<TestMessage>() &&
                        handler3.HandledMessages.ContainsNo<TestMessage>());
        }
    }
}