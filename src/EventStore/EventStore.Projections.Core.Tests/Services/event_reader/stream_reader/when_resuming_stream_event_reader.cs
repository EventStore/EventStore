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
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.TimerService;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;
using ResolvedEvent = EventStore.Core.Data.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.event_reader.stream_reader
{
    [TestFixture]
    public class when_resuming_stream_event_reader : TestFixtureWithExistingEvents
    {
        private StreamEventReader _edp;
        private Guid _distibutionPointCorrelationId;

        [SetUp]
        public new void When()
        {
            _distibutionPointCorrelationId = Guid.NewGuid();
            _edp = new StreamEventReader(
                _bus, _distibutionPointCorrelationId, null, "stream", 10, new RealTimeProvider(), false);
            _edp.Resume();
        }

        [Test, ExpectedException(typeof (InvalidOperationException))]
        public void it_cannot_be_resumed()
        {
            _edp.Resume();
        }

        [Test]
        public void it_cannot_be_paused()
        {
            _edp.Pause();
        }

        [Test]
        public void it_publishes_read_events_from_beginning()
        {
            Assert.AreEqual(1, _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>().Count());
            Assert.AreEqual(
                "stream", _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>().Single().EventStreamId);
            Assert.AreEqual(
                10, _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>().Single().FromEventNumber);
        }

        [Test]
        public void can_handle_read_events_completed()
        {
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "stream", 100, 100, ReadStreamResult.Success, 
                    new[]
                    {
                        new ResolvedEvent(new EventRecord(
                            10, 50, Guid.NewGuid(), Guid.NewGuid(), 50, 0, "stream", ExpectedVersion.Any, DateTime.UtcNow,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type", new byte[0], new byte[0]))
                    }, null, false, "", 11, 10, true, 100));
        }
    }
}
