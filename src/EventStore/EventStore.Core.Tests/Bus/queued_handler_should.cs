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
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Bus.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Bus
{
    [TestFixture]
    public abstract class queued_handler_should : QueuedHandlerTestWithNoopConsumer
    {
        protected queued_handler_should(Func<IHandle<Message>, string, TimeSpan, IQueuedHandler> queuedHandlerFactory)
                : base(queuedHandlerFactory)
        {
        }

        [Test]
        public void throw_if_handler_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => new QueuedHandler(null, "throwing", watchSlowMsg: false));
        }

        [Test]
        public void throw_if_name_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => new QueuedHandler(Consumer, null, watchSlowMsg: false));
        }
    }

    [TestFixture]
    public class queued_handler_mres_should: queued_handler_should
    {
        public queued_handler_mres_should()
                : base((consumer, name, timeout) => new QueuedHandlerMRES(consumer, name, false, null, timeout))
        {
        }
    }

    [TestFixture]
    public class queued_handler_autoreset_should : queued_handler_should
    {
        public queued_handler_autoreset_should()
            : base((consumer, name, timeout) => new QueuedHandlerAutoReset(consumer, name, false, null, timeout))
        {
        }
    }

    [TestFixture]
    public class queued_handler_sleep_should : queued_handler_should
    {
        public queued_handler_sleep_should()
            : base((consumer, name, timeout) => new QueuedHandlerSleep(consumer, name, false, null, timeout))
        {
        }
    }

    [TestFixture]
    public class queued_handler_pulse_should : queued_handler_should
    {
        public queued_handler_pulse_should()
            : base((consumer, name, timeout) => new QueuedHandlerPulse(consumer, name, false, null, timeout))
        {
        }
    }

    [TestFixture]
    public class queued_handler_threadpool_should : queued_handler_should
    {
        public queued_handler_threadpool_should()
            : base((consumer, name, timeout) => new QueuedHandlerThreadPool(consumer, name, false, null, timeout))
        {
        }
    }
}
