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
using EventStore.Core.Messages;
using EventStore.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.projection_checkpoint
{
    [TestFixture]
    public class when_emitting_events_in_correct_order_the_started_projection_checkpoint : TestFixtureWithExistingEvents
    {
        private ProjectionCheckpoint _checkpoint;
        private TestCheckpointManagerMessageHandler _readyHandler;

        protected override void Given()
        {
            AllWritesQueueUp();
            AllWritesToSucceed("$$stream1");
            AllWritesToSucceed("$$stream2");
            AllWritesToSucceed("$$stream3");
            NoOtherStreams();
        }

        [SetUp]
        public void setup()
        {
            _readyHandler = new TestCheckpointManagerMessageHandler();
            _checkpoint = new ProjectionCheckpoint(
                _ioDispatcher, new ProjectionVersion(1, 0, 0), null, _readyHandler,
                CheckpointTag.FromPosition(0, 100, 50), new TransactionFilePositionTagger(0),
                CheckpointTag.FromPosition(0, 0, -1), 250);
            _checkpoint.Start();
            _checkpoint.ValidateOrderAndEmitEvents(
                new[]
                {
                    new EmittedEventEnvelope(
                        new EmittedDataEvent(
                            "stream2", Guid.NewGuid(), "type1", true, "data2", null, CheckpointTag.FromPosition(0, 120, 110), null)),
                    new EmittedEventEnvelope(
                        new EmittedDataEvent(
                            "stream3", Guid.NewGuid(), "type2", true, "data3", null, CheckpointTag.FromPosition(0, 120, 110), null)),
                    new EmittedEventEnvelope(
                        new EmittedDataEvent(
                            "stream2", Guid.NewGuid(), "type3", true, "data4", null, CheckpointTag.FromPosition(0, 120, 110), null)),
                });
            _checkpoint.ValidateOrderAndEmitEvents(
                new[]
                {
                    new EmittedEventEnvelope(
                        new EmittedDataEvent(
                            "stream1", Guid.NewGuid(), "type4", true, "data", null, CheckpointTag.FromPosition(0, 140, 130), null))
                });
        }

        [Test]
        public void should_publish_write_events()
        {
            var writeEvents =
                _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>()
                         .ExceptOfEventType(SystemEventTypes.StreamMetadata);
            Assert.AreEqual(4, writeEvents.Count());
        }

        [Test]
        public void should_publish_write_events_to_correct_streams()
        {
            Assert.IsTrue(
                _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Any(v => v.EventStreamId == "stream1"));
            Assert.IsTrue(
                _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Any(v => v.EventStreamId == "stream2"));
            Assert.IsTrue(
                _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Any(v => v.EventStreamId == "stream3"));
        }

        [Test]
        public void should_group_events_to_the_same_stream_caused_by_the_same_event()
        {
            // this is important for the projection to be able to recover by CausedBy.  Unless we commit all the events
            // to the stream in a single transaction we can get into situation when only part of events CausedBy the same event
            // are present in a stream
            Assert.AreEqual(
                2,
                _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Single(v => v.EventStreamId == "stream2").
                    Events.Length);
        }

        [Test]
        public void should_not_write_a_secong_group_until_the_first_write_completes()
        {
            _checkpoint.ValidateOrderAndEmitEvents(
                new[]
                {
                    new EmittedEventEnvelope(
                        new EmittedDataEvent(
                            "stream1", Guid.NewGuid(), "type", true, "data", null, CheckpointTag.FromPosition(0, 170, 160), null))
                });
            var writeRequests =
                _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Where(v => v.EventStreamId == "stream1");
            var writeEvents = writeRequests.Single();
            writeEvents.Envelope.ReplyWith(new ClientMessage.WriteEventsCompleted(writeEvents.CorrelationId, 0, 0));
            Assert.AreEqual(2, writeRequests.Count());
        }
    }
}
