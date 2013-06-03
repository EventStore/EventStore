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

namespace EventStore.Projections.Core.Tests.Services.event_reader.multi_stream_reader
{
    [TestFixture]
    public class when_handling_read_completed_for_all_streams_then_pause_requested_then_eof :
        TestFixtureWithExistingEvents
    {
        private MultiStreamEventReader _edp;
        private Guid _publishWithCorrelationId;
        private Guid _distibutionPointCorrelationId;
        private Guid _firstEventId;
        private Guid _secondEventId;
        private Guid _thirdEventId;
        private Guid _fourthEventId;

        protected override void Given()
        {
            TicksAreHandledImmediately();
        }

        private string[] _abStreams;
        private Dictionary<string, int> _ab12Tag;

        [SetUp]
        public void When()
        {
            _ab12Tag = new Dictionary<string, int> {{"a", 1}, {"b", 2}};
            _abStreams = new[] {"a", "b"};

            _publishWithCorrelationId = Guid.NewGuid();
            _distibutionPointCorrelationId = Guid.NewGuid();
            _edp = new MultiStreamEventReader(
                _bus, _distibutionPointCorrelationId, null, _abStreams, _ab12Tag, false, new RealTimeProvider());
            _edp.Resume();
            _firstEventId = Guid.NewGuid();
            _secondEventId = Guid.NewGuid();
            _thirdEventId = Guid.NewGuid();
            _fourthEventId = Guid.NewGuid();
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "a", 100, 100, ReadStreamResult.Success, 
                    new[]
                        {
                            new ResolvedEvent(
                        new EventRecord(
                            1, 50, Guid.NewGuid(), _firstEventId, 50, 0, "a", ExpectedVersion.Any, DateTime.UtcNow,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type1", new byte[] {1}, new byte[] {2}), null),
                            new ResolvedEvent(
                        new EventRecord(
                            2, 100, Guid.NewGuid(), _secondEventId, 100, 0, "a", ExpectedVersion.Any, DateTime.UtcNow,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type2", new byte[] {3}, new byte[] {4}), null)
                        }, null, "", 3, 2, true, 200));
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "b", 100, 100, ReadStreamResult.Success, 
                    new[]
                        {
                            new ResolvedEvent(
                        new EventRecord(
                            2, 150, Guid.NewGuid(), _thirdEventId, 150, 0, "b", ExpectedVersion.Any, DateTime.UtcNow,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type1", new byte[] {1}, new byte[] {2}), null),
                            new ResolvedEvent(
                        new EventRecord(
                            3, 200, Guid.NewGuid(), _fourthEventId, 200, 0, "b", ExpectedVersion.Any, DateTime.UtcNow,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type2", new byte[] {3}, new byte[] {4}), null)
                        }, null, "", 4, 3, true, 200));
            _edp.Pause();
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "a", 100, 100, ReadStreamResult.Success, new ResolvedEvent[] { }, null, "", 3, 2, true, 400));
        }

        [Test]
        public void publishes_read_events_from_beginning_with_correct_next_event_number()
        {
            Assert.AreEqual(3, _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>().Count());
            Assert.IsTrue(
                _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>()
                         .Any(m => m.EventStreamId == "a"));
            Assert.IsTrue(
                _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>()
                         .Any(m => m.EventStreamId == "b"));
            Assert.AreEqual(
                3,
                _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>()
                         .Last(m => m.EventStreamId == "a")
                         .FromEventNumber);
            Assert.AreEqual(
                2,
                _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>()
                         .Last(m => m.EventStreamId == "b")
                         .FromEventNumber);
        }

        [Test]
        public void does_not_publish_schedule()
        {
            Assert.AreEqual(0, _consumer.HandledMessages.OfType<TimerMessage.Schedule>().Count());
        }

    }
}
