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
using System.Net;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication
{
    public abstract class RequestManagerSpecification
    {
        protected TwoPhaseRequestManagerBase manager;
        protected abstract TwoPhaseRequestManagerBase OnManager(FakePublisher publisher);
        protected List<Message> produced;
        protected abstract IEnumerable<Message> WithInitialMessages();
        protected FakePublisher _publisher;
        protected Guid CorrelationId = Guid.NewGuid();
        protected byte[] Metadata = new byte[255];
        protected byte[] EventData = new byte[255];
        protected FakeEnvelope Envelope;
        protected abstract Message When();

        protected Event DummyEvent()
        {
            return new Event(Guid.NewGuid(), "test", false, EventData, Metadata);
        }

        [SetUp]
        public void Setup()
        {
            _publisher = new FakePublisher();
            Envelope = new FakeEnvelope();
            manager = OnManager(_publisher);
            foreach(var m in WithInitialMessages())
            {
                manager.AsDynamic().Handle(m);
            }
            _publisher.Messages.Clear();
            manager.AsDynamic().Handle(When());
            produced = new List<Message>(_publisher.Messages);
        }
    }
}