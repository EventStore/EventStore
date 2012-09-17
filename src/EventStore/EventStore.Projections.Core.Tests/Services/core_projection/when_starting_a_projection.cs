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
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    [TestFixture]
    public class when_starting_a_projection
    {
        private const string _projectionStateStream = "$projections-projection-state";
        private const string _projectionCheckpointStream = "$projections-projection-checkpoint";
        private CoreProjection _coreProjection;
        private InMemoryBus _bus;
        private TestMessageHandler<ClientMessage.ReadEventsBackwards> _listEventsHandler;

        [SetUp]
        public void setup()
        {
            _bus = new InMemoryBus("bus");
            _listEventsHandler = new TestMessageHandler<ClientMessage.ReadEventsBackwards>();
            _bus.Subscribe(_listEventsHandler);
            _coreProjection = new CoreProjection(
                "projection", Guid.NewGuid(), _bus, new FakeProjectionStateHandler(),
                new ProjectionConfig(ProjectionMode.AdHoc, 5, 10, 1000, 250, true, true, true));
            _coreProjection.Start();
        }

        [Test]
        public void should_request_state_snapshot()
        {
            Assert.IsTrue(_listEventsHandler.HandledMessages.Count == 1);
        }

        [Test]
        public void should_request_state_snapshot_on_correct_stream()
        {
            Assert.AreEqual(_projectionCheckpointStream, _listEventsHandler.HandledMessages[0].EventStreamId);
        }

        [Test]
        public void should_accept_no_event_stream_response()
        {
            _coreProjection.Handle(
                new ClientMessage.ReadEventsBackwardsCompleted(
                    _listEventsHandler.HandledMessages[0].CorrelationId,
                    _listEventsHandler.HandledMessages[0].EventStreamId, new EventRecord[0], null,
                    RangeReadResult.NoStream, -1, 1000));
        }

        [Test]
        public void should_accept_events_not_found_response()
        {
            _coreProjection.Handle(
                new ClientMessage.ReadEventsBackwardsCompleted(
                    _listEventsHandler.HandledMessages[0].CorrelationId,
                    _listEventsHandler.HandledMessages[0].EventStreamId, new EventRecord[0], null,
                    RangeReadResult.Success, -1, 1000));
        }
    }
}
