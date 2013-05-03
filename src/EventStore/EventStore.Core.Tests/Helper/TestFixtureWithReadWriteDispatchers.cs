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
using System.IO;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Bus.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Helper
{
    public abstract class TestFixtureWithReadWriteDispatchers
    {
        protected InMemoryBus _bus;

        protected RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>
            _writeDispatcher;

        protected
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>
            _readDispatcher;

        protected TestHandler<Message> _consumer;
        protected IODispatcher _ioDispatcher;
        protected ManualQueue _queue;

        [SetUp]
        public void setup0()
        {
            _bus = new InMemoryBus("bus");
            _ioDispatcher = new IODispatcher(_bus, new PublishEnvelope(GetInputQueue()));
            _readDispatcher = _ioDispatcher.BackwardReader;
            _writeDispatcher = _ioDispatcher.Writer;

            _bus.Subscribe(_ioDispatcher.ForwardReader);
            _bus.Subscribe(_ioDispatcher.BackwardReader);
            _bus.Subscribe(_ioDispatcher.ForwardReader);
            _bus.Subscribe(_ioDispatcher.Writer);
            _bus.Subscribe(_ioDispatcher.StreamDeleter);

            _consumer = new TestHandler<Message>();
            _bus.Subscribe(_consumer);
        }

        protected virtual IPublisher GetInputQueue()
        {
            return _bus;
        }

        protected void SetUpManualQueue()
        {
            _queue = new ManualQueue(_bus);
        }

        protected void WhenLoop()
        {
            _queue.Process();
            foreach (var action in When())
            {
                action();
                _queue.Process();
            }
        }

        protected virtual IEnumerable<Action> When()
        {
            yield break;
        }
    }
}
