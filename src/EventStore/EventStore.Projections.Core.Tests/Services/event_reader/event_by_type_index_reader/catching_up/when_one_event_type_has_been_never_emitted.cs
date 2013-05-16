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

namespace EventStore.Projections.Core.Tests.Services.event_reader.event_by_type_index_reader.catching_up
{
    namespace when_one_event_type_has_been_never_emitted
    {
        abstract class with_one_event_type_has_been_never_emitted : TestFixtureWithEventReaderService
        {
            protected const int TailLength = 10;
            protected Guid _subscriptionId;
            private QuerySourcesDefinition _sourceDefinition;
            protected IReaderStrategy _readerStrategy;
            protected ReaderSubscriptionOptions _readerSubscriptionOptions;
            protected TFPos _tfPos1;
            protected TFPos _tfPos2;
            protected TFPos _tfPos3;
            protected TFPos[] _tfPos;

            protected override bool GivenHeadingReaderRunning()
            {
                return true;
            }

            protected override void Given()
            {
                base.Given();
                AllWritesSucceed();
                _tfPos = new TFPos[TailLength];
                _tfPos1 = ExistingEvent("test-stream", "type1", "{}", "{Data: 1}");
                _tfPos2 = ExistingEvent("test-stream", "type1", "{}", "{Data: 2}");
                _tfPos3 = ExistingEvent("test-stream", "type1", "{}", "{Data: 3}");

                for (var i = 0; i < TailLength; i++)
                {
                    _tfPos[i] = ExistingEvent("test-stream", "type1", "{}", "{Data: " + i + "}");
                }

                GivenInitialIndexState();

                _subscriptionId = Guid.NewGuid();
                _sourceDefinition = new QuerySourcesDefinition
                    {
                        AllStreams = true,
                        Events = new[] {"type1", "type2"},
                        Options = new QuerySourcesDefinitionOptions {}
                    };
                _readerStrategy = ReaderStrategy.Create(_sourceDefinition, _timeProvider, runAs: null);
                _readerSubscriptionOptions = new ReaderSubscriptionOptions(
                    checkpointUnhandledBytesThreshold: 10000, checkpointProcessedEventsThreshold: 100, stopOnEof: false,
                    stopAfterNEvents: null);
            }

            protected abstract void GivenInitialIndexState();

            protected string TFPosToMetadata(TFPos tfPos)
            {
                return string.Format(@"{{""$c"":{0},""$p"":{1}}}", tfPos.CommitPosition, tfPos.PreparePosition);
            }

            [Test]
            public void returns_all_events()
            {
                var receivedEvents =
                    _consumer.HandledMessages.OfType<EventReaderSubscriptionMessage.CommittedEventReceived>().ToArray();

                Assert.AreEqual(TailLength + 3, receivedEvents.Length);
            }

            [Test]
            public void returns_events_in_original_order()
            {
                var receivedEvents =
                    _consumer.HandledMessages.OfType<EventReaderSubscriptionMessage.CommittedEventReceived>().ToArray();

                Assert.That(
                    (from e in receivedEvents
                     orderby e.Data.EventSequenceNumber
                     select e.Data.EventSequenceNumber).SequenceEqual(
                         from e in receivedEvents
                         select e.Data.EventSequenceNumber), "Incorrect event order received");
            }
        }

        [TestFixture]
        class when_index_checkpoint_multiple_events_behind : with_one_event_type_has_been_never_emitted
        {
            protected override void GivenInitialIndexState()
            {
                ExistingEvent("$et-type1", "$>", TFPosToMetadata(_tfPos1), "0@test-stream");
                ExistingEvent("$et-type1", "$>", TFPosToMetadata(_tfPos2), "1@test-stream");
                ExistingEvent("$et-type1", "$>", TFPosToMetadata(_tfPos3), "2@test-stream");

                for (var i = 0; i < TailLength; i++)
                    ExistingEvent("$et-type1", "$>", TFPosToMetadata(_tfPos[i]), (i + 3)+ "@test-stream");

                NoStream("$et-type2");
                ExistingEvent("$et", "$Checkpoint", TFPosToMetadata(_tfPos3), TFPosToMetadata(_tfPos3));
            }

            protected override IEnumerable<WhenStep> When()
            {
                var fromZeroPosition = CheckpointTag.FromEventTypeIndexPositions(
                    new TFPos(0, -1), new Dictionary<string, int> {{"type1", -1}, {"type2", -1}});
                yield return
                    new ReaderSubscriptionManagement.Subscribe(
                        _subscriptionId, fromZeroPosition, _readerStrategy, _readerSubscriptionOptions);

            }
        }

    }
}
