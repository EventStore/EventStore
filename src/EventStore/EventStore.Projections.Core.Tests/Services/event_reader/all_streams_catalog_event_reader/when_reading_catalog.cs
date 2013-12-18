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
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_reader.all_streams_catalog_event_reader
{
    namespace when_reading_catalog
    {
        abstract class with_all_streams_catelog_event_reader: TestFixtureWithEventReaderService
        {
            protected const int TailLength = 10;
            protected Guid _subscriptionId;
            private QuerySourcesDefinition _sourceDefinition;
            protected IReaderStrategy _readerStrategy;
            protected ReaderSubscriptionOptions _readerSubscriptionOptions;
            protected TFPos _tfPos1;
            protected TFPos _tfPos2;
            protected TFPos _tfPos3;

            protected override bool GivenHeadingReaderRunning()
            {
                return false;
            }

            protected override void Given()
            {
                base.Given();
                AllWritesSucceed();
                _tfPos1 = ExistingEvent("test-stream", "type1", "{}", "{Data: 1}");
                _tfPos2 = ExistingEvent("test-stream", "type1", "{}", "{Data: 2}");
                _tfPos3 = ExistingEvent("test-stream2", "type1", "{}", "{Data: 3}");
                ExistingEvent("test-stream2", "type1", "{}", "{Data: 4}");
                ExistingEvent("test-stream3", "type1", "{}", "{Data: 5}");
                ExistingEvent("test-stream3", "type1", "{}", "{Data: 6}");
                ExistingEvent("test-stream4", "type1", "{}", "{Data: 7}");

                ExistingEvent("$$test-stream", "$metadata", "{Meta: 1}", "");
                ExistingEvent("$$test-stream2", "$metadata", "{Meta: 2}", "");
                ExistingEvent("$$test-stream3", "$metadata", "{Meta: 3}", "");


                ExistingEvent("$streams", "$>", null, "0@test-stream");
                ExistingEvent("$streams", "$>", null, "0@test-stream2");
                ExistingEvent("$streams", "$>", null, "0@test-stream3");
                ExistingEvent("$streams", "$>", null, "0@test-stream4");
                NoOtherStreams();

                _subscriptionId = Guid.NewGuid();
                _sourceDefinition = new QuerySourcesDefinition
                {
                    CatalogStream = "$all",
                    AllEvents = true,
                    ByStreams = true,
                    Options = new QuerySourcesDefinitionOptions { }
                };
                _readerStrategy = new ParallelQueryAllStreamsMasterReaderStrategy(
                    0, SystemAccount.Principal, _timeProvider);
                _readerSubscriptionOptions = new ReaderSubscriptionOptions(
                    checkpointUnhandledBytesThreshold: 10000, checkpointProcessedEventsThreshold: 100, stopOnEof: true,
                    stopAfterNEvents: null);
            }

            [Test]
            public void returns_all_catalog_events()
            {
                var receivedEvents =
                    _consumer.HandledMessages.OfType<EventReaderSubscriptionMessage.CommittedEventReceived>().ToArray();

                Assert.AreEqual(4, receivedEvents.Length);
            }

            [Test]
            public void events_are_correct()
            {
                var receivedEvents =
                    _consumer.HandledMessages.OfType<EventReaderSubscriptionMessage.CommittedEventReceived>().ToArray();
                var first = receivedEvents[0];
                var second = receivedEvents[1];
                var third = receivedEvents[2];
                var fourth = receivedEvents[3];

                Assert.AreEqual(true, first.Data.ResolvedLinkTo);
                Assert.AreEqual("{Meta: 1}", first.Data.Metadata);
                Assert.AreEqual(true, second.Data.ResolvedLinkTo);
                Assert.AreEqual("{Meta: 2}", second.Data.Metadata);
                Assert.AreEqual(true, third.Data.ResolvedLinkTo);
                Assert.AreEqual("{Meta: 3}", third.Data.Metadata);
                Assert.AreEqual(false, fourth.Data.ResolvedLinkTo);
            }

            [Test]
            public void returns_catalog_events_in_catalog_order()
            {
                var receivedEvents =
                    _consumer.HandledMessages.OfType<EventReaderSubscriptionMessage.CommittedEventReceived>().ToArray();

                Assert.That(
                    (from e in receivedEvents orderby e.Data.Position select e.Data.Position)
                        .SequenceEqual(from e in receivedEvents select e.Data.Position),
                    "Incorrect event order received");
            }

        }

        [TestFixture]
        class when_starting_from_the_beginning : with_all_streams_catelog_event_reader
        {
            protected override IEnumerable<WhenStep> When()
            {
                var fromZeroPosition = CheckpointTag.FromByStreamPosition(0, "", -1, null, -1, 100000);
                yield return
                    new ReaderSubscriptionManagement.Subscribe(
                        _subscriptionId, fromZeroPosition, _readerStrategy, _readerSubscriptionOptions);
            }
        }

    }
}
