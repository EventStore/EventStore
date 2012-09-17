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
using EventStore.Core.Tests.Bus.QueuedHandler.Helpers;
using EventStore.Core.Tests.Common;
using NUnit.Framework;

namespace EventStore.Core.Tests.Bus.QueuedHandler
{
    [TestFixture]
    public class when_publishing_before_starting : QueuedHandlerTestWithWaitingConsumer
    {
        [Test]
        public void should_not_throw()
        {
            Assert.DoesNotThrow(() => _queue.Publish(new TestMessage()));
        }

        [Test]
        public void should_not_forward_message_to_bus()
        {
            _consumer.SetWaitingCount(1);

            _queue.Publish(new TestMessage());

            _consumer.Wait(10);

            Assert.That(_consumer.HandledMessages.ContainsNo<TestMessage>());
        }

        [Test]
        public void and_then_starting_message_should_be_forwarded_to_bus()
        {
            _consumer.SetWaitingCount(1);

            _queue.Publish(new TestMessage());
            try
            {
                _queue.Start();
                _consumer.Wait();
            }
            finally
            {
                _queue.Stop();
            }

            Assert.That(_consumer.HandledMessages.ContainsSingle<TestMessage>());
        }

        [Test]
        public void multiple_messages_and_then_starting_messages_should_be_forwarded_to_bus()
        {
            _consumer.SetWaitingCount(3);

            _queue.Publish(new TestMessage());
            _queue.Publish(new TestMessage2());
            _queue.Publish(new TestMessage3());

            try
            {
                _queue.Start();
                _consumer.Wait();
            }
            finally
            {
                _queue.Stop();
            }

            Assert.That(_consumer.HandledMessages.ContainsSingle<TestMessage>() &&
                        _consumer.HandledMessages.ContainsSingle<TestMessage2>() &&
                        _consumer.HandledMessages.ContainsSingle<TestMessage3>());
        }
    }
}