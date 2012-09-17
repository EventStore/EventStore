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
    public class when_publishing : BusTestBase
    {
        [Test]
        public void null_message_app_should_throw()
        {
            Assert.Throws<ArgumentNullException>(() => Bus.Publish(null));
        }

        [Test]
        public void unsubscribed_messages_noone_should_handle_it()
        {
            var handler1 = new TestHandler();
            var handler2 = new TestHandler2();
            var handler3 = new TestHandler3();

            Bus.Publish(new TestMessage());
            Bus.Publish(new TestMessage2());
            Bus.Publish(new TestMessage3());

            Assert.That(handler1.DidntHandleAnyMessages() &&
                        handler2.DidntHandleAnyMessages() &&
                        handler3.DidntHandleAnyMessages());
        }

        [Test]
        public void any_message_no_other_messages_should_be_published()
        {
            var handler1 = new TestHandler();
            var handler2 = new TestHandler2();

            Bus.Subscribe<TestMessage>(handler1);
            Bus.Subscribe<TestMessage2>(handler2);

            Bus.Publish(new TestMessage());

            Assert.That(handler1.HandledMessages.ContainsSingle<TestMessage>() &&
                        handler2.DidntHandleAnyMessages());
        }

        [Test]
        public void same_message_n_times_it_should_be_handled_n_times()
        {
            var handler = new MessageWithIdHandler();
            var message = new TestMessageWithId(11);

            Bus.Subscribe<TestMessageWithId>(handler);

            Bus.Publish(message);
            Bus.Publish(message);
            Bus.Publish(message);

            Assert.That(handler.HandledMessages.ContainsN<TestMessageWithId>(3, mes => mes.Id == 11));
        }
        
        [Test]
        public void multiple_messages_of_same_type_they_all_should_be_delivered()
        {
            var handler = new MessageWithIdHandler();
            var message1 = new TestMessageWithId(1);
            var message2 = new TestMessageWithId(2);
            var message3 = new TestMessageWithId(3);

            Bus.Subscribe<TestMessageWithId>(handler);

            Bus.Publish(message1);
            Bus.Publish(message2);
            Bus.Publish(message3);

            Assert.That(handler.HandledMessages.ContainsSingle<TestMessageWithId>(mes => mes.Id == 1));
            Assert.That(handler.HandledMessages.ContainsSingle<TestMessageWithId>(mes => mes.Id == 2));
            Assert.That(handler.HandledMessages.ContainsSingle<TestMessageWithId>(mes => mes.Id == 3));
        }

        [Test]
        public void message_of_child_type_then_all_subscribed_handlers_of_parent_type_should_handle_message()
        {
            var parentHandler = new ParentTestHandler();
            Bus.Subscribe<ParentTestMessage>(parentHandler);

            Bus.Publish(new ChildTestMessage());

            Assert.That(parentHandler.HandledMessages.ContainsSingle<ChildTestMessage>());
        }

        [Test]
        public void message_of_parent_type_then_no_subscribed_handlers_of_child_type_should_handle_message()
        {
            var childHandler = new ChildTestHandler();
            Bus.Subscribe<ChildTestMessage>(childHandler);

            Bus.Publish(new ParentTestMessage());

            Assert.That(childHandler.HandledMessages.ContainsNo<ParentTestMessage>());
        }

        [Test]
        public void message_of_grand_child_type_then_all_subscribed_handlers_of_base_types_should_handle_message()
        {
            var parentHandler = new ParentTestHandler();
            var childHandler = new ChildTestHandler();

            Bus.Subscribe<ParentTestMessage>(parentHandler);
            Bus.Subscribe<ChildTestMessage>(childHandler);

            Bus.Publish(new GrandChildTestMessage());

            Assert.That(parentHandler.HandledMessages.ContainsSingle<GrandChildTestMessage>() && 
                        childHandler.HandledMessages.ContainsSingle<GrandChildTestMessage>());
        }

        [Test]
        public void message_of_grand_child_type_then_all_subscribed_handlers_of_parent_types_including_grand_child_handler_should_handle_message()
        {
            var parentHandler = new ParentTestHandler();
            var childHandler = new ChildTestHandler();
            var grandChildHandler = new GrandChildTestHandler();

            Bus.Subscribe<ParentTestMessage>(parentHandler);
            Bus.Subscribe<ChildTestMessage>(childHandler);
            Bus.Subscribe<GrandChildTestMessage>(grandChildHandler);

            Bus.Publish(new GrandChildTestMessage());

            Assert.That(parentHandler.HandledMessages.ContainsSingle<GrandChildTestMessage>() && 
                        childHandler.HandledMessages.ContainsSingle<GrandChildTestMessage>() &&
                        grandChildHandler.HandledMessages.ContainsSingle<GrandChildTestMessage>());
        }

    }
}