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

namespace EventStore.Projections.Core.Tests.Services.event_reader.multi_stream_reader
{
    namespace reordering
    {
        abstract class with_multi_stream_reader : TestFixtureWithEventReaderService
        {
            protected Guid _subscriptionId;
            private QuerySourcesDefinition _sourceDefinition;
            protected IReaderStrategy _readerStrategy;
            protected ReaderSubscriptionOptions _readerSubscriptionOptions;
            protected TFPos _tfPos1;
            protected TFPos _tfPos2;

            protected override bool GivenHeadingReaderRunning()
            {
                return true;
            }

            protected override void Given()
            {
                base.Given();
                AllWritesSucceed();
                _tfPos1 = ExistingEvent("stream-a", "type1", "{}", "{Data: 1}");
                _tfPos2 = ExistingEvent("stream-b", "type1", "{}", "{Data: 2}");

                GivenOtherEvents();

                _subscriptionId = Guid.NewGuid();
                _sourceDefinition = new QuerySourcesDefinition
                    {
                        Streams =  new []{"stream-a", "stream-b"},
                        AllEvents = true,
                        Options = new QuerySourcesDefinitionOptions {ReorderEvents = true, ProcessingLag = 100}
                    };
                _readerStrategy = ReaderStrategy.Create(0, _sourceDefinition, _timeProvider, runAs: null);
                _readerSubscriptionOptions = new ReaderSubscriptionOptions(
                    checkpointUnhandledBytesThreshold: 10000, checkpointProcessedEventsThreshold: 100, stopOnEof: false,
                    stopAfterNEvents: null);
            }

            protected abstract void GivenOtherEvents();

            protected string TFPosToMetadata(TFPos tfPos)
            {
                return string.Format(@"{{""$c"":{0},""$p"":{1}}}", tfPos.CommitPosition, tfPos.PreparePosition);
            }

            [Test]
            public void returns_all_events()
            {
                var receivedEvents =
                    _consumer.HandledMessages.OfType<EventReaderSubscriptionMessage.CommittedEventReceived>().ToArray();

                Assert.AreEqual(5, receivedEvents.Length);
            }

            [Test]
            public void returns_events_in_original_order()
            {
                var receivedEvents =
                    _consumer.HandledMessages.OfType<EventReaderSubscriptionMessage.CommittedEventReceived>().ToArray();

                Assert.That(
                    (from e in receivedEvents
                     orderby e.Data.Position.PreparePosition
                     select e.Data.Position.PreparePosition).SequenceEqual(
                         from e in receivedEvents
                         select e.Data.Position.PreparePosition), "Incorrect event order received");
            }
        }

        [TestFixture]
        class when_event_commit_is_delayed : with_multi_stream_reader
        {
            protected override void GivenOtherEvents()
            {
            }

            protected override IEnumerable<WhenStep> When()
            {
                var fromZeroPosition =
                    CheckpointTag.FromStreamPositions(0, new Dictionary<string, int> {{"stream-a", -1}, {"stream-b", -1}});
                yield return
                    new ReaderSubscriptionManagement.Subscribe(
                        _subscriptionId, fromZeroPosition, _readerStrategy, _readerSubscriptionOptions);


                var correlationId = Guid.NewGuid();
                yield return
                    new ClientMessage.TransactionStart(
                        Guid.NewGuid(), correlationId, new PublishEnvelope(GetInputQueue()), true, "stream-a", 0, null);

                var transactionId =
                    _consumer.HandledMessages.OfType<ClientMessage.TransactionStartCompleted>()
                             .Single(m => m.CorrelationId == correlationId)
                             .TransactionId;

                correlationId = Guid.NewGuid();
                yield return
                    new ClientMessage.TransactionWrite(
                        Guid.NewGuid(), correlationId, new PublishEnvelope(GetInputQueue()), true, transactionId,
                        new[] {new Event(Guid.NewGuid(), "type1", true, "{Data: 3, Transacted=true}", "{}")}, null);

                correlationId = Guid.NewGuid();
                yield return
                    new ClientMessage.WriteEvents(
                        Guid.NewGuid(), correlationId, new PublishEnvelope(GetInputQueue()), true, "stream-b", 0,
                        new[] {new Event(Guid.NewGuid(), "type1", true, "{Data: 4}", "{}")}, null);

                correlationId = Guid.NewGuid();
                yield return
                    new ClientMessage.TransactionWrite(
                        Guid.NewGuid(), correlationId, new PublishEnvelope(GetInputQueue()), true, transactionId,
                        new[] {new Event(Guid.NewGuid(), "type1", true, "{Data: 5, Transacted=true}", "{}")}, null);

                correlationId = Guid.NewGuid();
                yield return
                    new ClientMessage.TransactionCommit(
                        Guid.NewGuid(), correlationId, new PublishEnvelope(GetInputQueue()), true, transactionId, null);

                yield return Yield;

                _timeProvider.AddTime(TimeSpan.FromMilliseconds(300));

                yield return Yield;

            }
        }

    }
}
