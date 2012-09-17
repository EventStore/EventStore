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
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Tests.Bus.QueuedHandler.Helpers;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services
{
    public class TestFixtureWithProjectionCoreService
    {
        public class TestCoreProjection : ICoreProjection
        {
            public List<ProjectionMessage.Projections.CommittedEventReceived> HandledMessages =
                new List<ProjectionMessage.Projections.CommittedEventReceived>();

            public List<ProjectionMessage.Projections.CheckpointSuggested> HandledCheckpoints =
                new List<ProjectionMessage.Projections.CheckpointSuggested>();

            public void Handle(ProjectionMessage.Projections.CommittedEventReceived message)
            {
                HandledMessages.Add(message);
            }

            public void Handle(ProjectionMessage.Projections.CheckpointSuggested message)
            {
                HandledCheckpoints.Add(message);
            }
        }

        protected WatchingConsumer _consumer;
        protected InMemoryBus _bus;
        protected ProjectionCoreService _service;

        [SetUp]
        public void Setup()
        {
            _consumer = new WatchingConsumer();
            _bus = new InMemoryBus("temp");
            _bus.Subscribe(_consumer);
            _service = new ProjectionCoreService(_bus, 10, new InMemoryCheckpoint(1000));
            _service.Handle(new ProjectionMessage.CoreService.Start());
        }

        protected CheckpointStrategy CreateCheckpointStrategy()
        {
            var result = new CheckpointStrategy.Builder();
            result.FromAll();
            result.AllEvents();
            return result.Build();
        }

        protected static Event CreateEvent()
        {
            return new Event(Guid.NewGuid(), "t", false, new byte[0], new byte[0]);
        }
    }
}
