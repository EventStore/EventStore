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
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using System.Linq;

namespace EventStore.Projections.Core.Tests.Services.event_reader.multi_stream_reader.catching_up
{
    [TestFixture]
    class when_reordering_happens_in_event_by_type_index: TestFixtureWithEventReaderService
    {
        private Guid _subscriptionId;
        private QuerySourcesDefinition _sourceDefinition;
        private IReaderStrategy _readerStrategy;
        private ReaderSubscriptionOptions _readerSubscriptionOptions;
        private TFPos _tfPos1;
        private TFPos _tfPos2;
        private TFPos _tfPos3;


        protected override bool GivenHeadingReaderRunning()
        {
            return true;
        }

        protected override void Given()
        {
            base.Given();
            AllWritesSucceed();

            _tfPos1 = ExistingEvent("test-stream", "type1", "{}", "{Data: 1}");
            _tfPos2 = ExistingEvent("test-stream", "type1", "{}", "{Data: 2}");
            _tfPos3 = ExistingEvent("test-stream", "type2", "{}", "{Data: 3}");

            NoStream("$et-type1");
            NoStream("$et-type2");

            _subscriptionId = Guid.NewGuid();
            _sourceDefinition = new QuerySourcesDefinition
                {
                    AllStreams = true,
                    Events = new[] {"type1", "type2"},
                    Options = new QuerySourcesDefinitionOptions {UseEventIndexes = true}
                };
            _readerStrategy = ReaderStrategy.Create(_sourceDefinition);
            _readerSubscriptionOptions = new ReaderSubscriptionOptions(
                checkpointUnhandledBytesThreshold: 10000, checkpointProcessedEventsThreshold: 100, stopOnEof: false,
                stopAfterNEvents: null);
        }

        private string TFPosToMetadata(TFPos tfPos)
        {
            return string.Format(@"{{""$c"":{0},""$p"":{1}}}", tfPos.CommitPosition, tfPos.PreparePosition);
        }

        protected override IEnumerable<Message> When()
        {
            var fromZeroPosition =
                CheckpointTag.FromStreamPositions(new Dictionary<string, int> {{"$et-type1", -1}, {"$et-type2", -1}});
            yield return
                new ReaderSubscriptionManagement.Subscribe(
                    _subscriptionId, fromZeroPosition, _readerStrategy, _readerSubscriptionOptions);

            // simulate index-by-type system projection
            yield return
                new ClientMessage.WriteEvents(
                    Guid.NewGuid(), new NoopEnvelope(), false, "$et-type1", ExpectedVersion.Any,
                    new Event(Guid.NewGuid(), "$>", false, "0@test-stream", TFPosToMetadata(_tfPos1)), user: null);

            // simulate index-by-type system projection (the second event write is delayed - awaiting for ACK from the previous write)
            yield return
                new ClientMessage.WriteEvents(
                    Guid.NewGuid(), new NoopEnvelope(), false, "$et-type2", ExpectedVersion.Any,
                    new Event(Guid.NewGuid(), "$>", false, "2@test-stream", TFPosToMetadata(_tfPos3)), user: null);

            // simulate index-by-type system projection (ACK received - writing the next event)
            yield return
                new ClientMessage.WriteEvents(
                    Guid.NewGuid(), new NoopEnvelope(), false, "$et-type1", ExpectedVersion.Any,
                    new Event(Guid.NewGuid(), "$>", false, "1@test-stream", TFPosToMetadata(_tfPos2)), user: null);

        }

        [Test, Ignore]
        public void returns_events_in_original_order()
        {
            var receivedEvents =
                _consumer.HandledMessages.OfType<EventReaderSubscriptionMessage.CommittedEventReceived>().ToArray();

            Assert.That(new []{0, 1, 2}.SequenceEqual(
                from e in receivedEvents
                select e.Data.EventSequenceNumber
                ), "Incorrect event order received");
        }
    }
}
