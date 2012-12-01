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
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.multi_stream_event_distribution_point
{
    [TestFixture]
    public class when_handling_read_completed : TestFixtureWithExistingEvents
    {
        private MultiStreamReaderEventDistributionPoint _edp;
        private Guid _publishWithCorrelationId;
        private Guid _distibutionPointCorrelationId;
        private Guid _firstEventId;
        private Guid _secondEventId;

        protected override void Given()
        {
            TicksAreHandledImmediately();
        }

        private string[] _abStreams;
        private Dictionary<string, int> _ab12Tag;

        [SetUp]
        public void When()
        {
            _ab12Tag = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };
            _abStreams = new[] { "a", "b" };

            _publishWithCorrelationId = Guid.NewGuid();
            _distibutionPointCorrelationId = Guid.NewGuid();
            _edp = new MultiStreamReaderEventDistributionPoint(_bus, _distibutionPointCorrelationId, _abStreams, _ab12Tag, false, new RealTimeProvider());
            _edp.Resume();
            _firstEventId = Guid.NewGuid();
            _secondEventId = Guid.NewGuid();
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "a",
                    new[]
                    {
                        new EventLinkPair( 
                            new EventRecord(
                                1, 50, Guid.NewGuid(), _firstEventId, 50, 0, "a", ExpectedVersion.Any, DateTime.UtcNow,
                                PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                                "event_type1", new byte[] {1}, new byte[] {2}), null),
                        new EventLinkPair( 
                            new EventRecord(
                                2, 100, Guid.NewGuid(), _secondEventId, 100, 0, "a", ExpectedVersion.Any, DateTime.UtcNow,
                                PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                                "event_type2", new byte[] {3}, new byte[] {4}), null)
                    }, RangeReadResult.Success, 3, 4, false, 200));
        }

        [Test, ExpectedException(typeof (InvalidOperationException))]
        public void cannot_be_resumed()
        {
            _edp.Resume();
        }

        [Test]
        public void cannot_be_paused()
        {
            _edp.Pause();
        }

        [Test]
        public void does_not_publish_committed_event_received_messages()
        {
            Assert.AreEqual(
                0, _consumer.HandledMessages.OfType<ProjectionCoreServiceMessage.CommittedEventDistributed>().Count());
        }

        [Test]
        public void publishes_read_events_from_beginning_with_correct_next_event_number()
        {
            // do not publish new read requests until we consume already available events
            Assert.AreEqual(2, _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>().Count());
            Assert.AreEqual(
                1,
                _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>()
                         .Last(v => v.EventStreamId == "a")
                         .FromEventNumber);
        }

        [Test, ExpectedException(typeof (InvalidOperationException))]
        public void cannot_handle_repeated_read_events_completed()
        {
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "a",
                    new[]
                    {
                        new EventLinkPair( 
                            new EventRecord(
                        2, 50, Guid.NewGuid(), Guid.NewGuid(), 50, 0, "a", ExpectedVersion.Any, DateTime.UtcNow,
                        PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                        "event_type", new byte[0], new byte[0]), null)
                    }, RangeReadResult.Success, 3, 4, false, 100));
        }

    }
}
