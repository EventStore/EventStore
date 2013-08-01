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
using EventStore.Core.Tests.Services.TimeService;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;
using ResolvedEvent = EventStore.Core.Data.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.event_reader.multi_stream_reader
{
    [TestFixture]
    public class when_onetime_reader_handles_eof : TestFixtureWithExistingEvents
    {
        private MultiStreamEventReader _edp;
        private Guid _distibutionPointCorrelationId;
        private Guid _firstEventId;
        private Guid _secondEventId;

        protected override void Given()
        {
            TicksAreHandledImmediately();
        }

        private string[] _abStreams;
        private Dictionary<string, int> _ab12Tag;
        private FakeTimeProvider _fakeTimeProvider;

        [SetUp]
        public new void When()
        {
            _ab12Tag = new Dictionary<string, int> {{"a", 1}, {"b", 2}};
            _abStreams = new[] {"a", "b"};

            _distibutionPointCorrelationId = Guid.NewGuid();
            _fakeTimeProvider = new FakeTimeProvider();
            _edp = new MultiStreamEventReader(
                _bus, _distibutionPointCorrelationId, null, _abStreams, _ab12Tag, false, _fakeTimeProvider,
                stopOnEof: true);
            _edp.Resume();
            _firstEventId = Guid.NewGuid();
            _secondEventId = Guid.NewGuid();
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "a", 100, 100, ReadStreamResult.Success, 
                    new[]
                        {
                            new ResolvedEvent(
                        new EventRecord(
                            1, 50, Guid.NewGuid(), _firstEventId, 50, 0, "a", ExpectedVersion.Any, _fakeTimeProvider.Now,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type1", new byte[] {1}, new byte[] {2}), null),
                        }, null, false, "", 2, 1, true, 200));
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "b", 100, 100, ReadStreamResult.Success, 
                    new[]
                        {
                            new ResolvedEvent(
                        new EventRecord(
                            2, 100, Guid.NewGuid(), _secondEventId, 100, 0, "b", ExpectedVersion.Any,
                            _fakeTimeProvider.Now,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type1", new byte[] {1}, new byte[] {2}), null),
                        }, null, false, "", 3, 2, true, 200));
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "a", 100, 100, ReadStreamResult.Success, new ResolvedEvent[] { }, null, false, "", 2, 1, true, 400));
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "b", 100, 100, ReadStreamResult.Success, new ResolvedEvent[] { }, null, false, "", 3, 2, true, 400));
        }

        [Test]
        public void publishes_eof_message()
        {
            Assert.AreEqual(1, _consumer.HandledMessages.OfType<ReaderSubscriptionMessage.EventReaderEof>().Count());
            var first = _consumer.HandledMessages.OfType<ReaderSubscriptionMessage.EventReaderEof>().First();
            Assert.AreEqual(first.CorrelationId, _distibutionPointCorrelationId);
        }

        [Test]
        public void does_not_publish_read_messages_anymore()
        {
            Assert.AreEqual(4, _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>().Count());
        }
    }
}
