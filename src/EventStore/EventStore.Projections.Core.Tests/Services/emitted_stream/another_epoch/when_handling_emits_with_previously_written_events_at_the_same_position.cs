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
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Core.Services;
using EventStore.Core.Tests.Helpers;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TestFixtureWithExistingEvents = EventStore.Projections.Core.Tests.Services.core_projection.TestFixtureWithExistingEvents;

namespace EventStore.Projections.Core.Tests.Services.emitted_stream.another_epoch
{
    [TestFixture]
    public class when_handling_emits_with_previously_written_events_at_the_same_position : TestFixtureWithExistingEvents
    {
        private EmittedStream _stream;
        private TestCheckpointManagerMessageHandler _readyHandler;
        private int _1;
        private int _2;
        private int _3;

        protected override void Given()
        {
            AllWritesQueueUp();
            AllWritesToSucceed("$$test_stream");
            //NOTE: it is possible for a batch of events to be partially written if it contains links 
            ExistingEvent("test_stream", "type1", @"{""v"": 1, ""c"": 100, ""p"": 50}", "data");
            ExistingEvent("test_stream", "type2", @"{""v"": 1, ""c"": 100, ""p"": 50}", "data");
            NoOtherStreams();
        }

        private EmittedEvent[] CreateEventBatch()
        {
            return new EmittedEvent[]
            {
                new EmittedDataEvent(
                    (string) "test_stream", Guid.NewGuid(), (string) "type1", (bool) true,
                    (string) "data", (ExtraMetaData) null, CheckpointTag.FromPosition(0, 100, 50), (CheckpointTag) null, v => _1 = v),
                new EmittedDataEvent(
                    (string) "test_stream", Guid.NewGuid(), (string) "type2", (bool) true,
                    (string) "data", (ExtraMetaData) null, CheckpointTag.FromPosition(0, 100, 50), (CheckpointTag) null, v => _2 = v),
                new EmittedDataEvent(
                    (string) "test_stream", Guid.NewGuid(), (string) "type3", (bool) true,
                    (string) "data", (ExtraMetaData) null, CheckpointTag.FromPosition(0, 100, 50), (CheckpointTag) null, v => _3 = v)
            };
        }

        [SetUp]
        public void setup()
        {
            _readyHandler = new TestCheckpointManagerMessageHandler();
            _stream = new EmittedStream(
                "test_stream", new EmittedStream.WriterConfiguration(new EmittedStream.WriterConfiguration.StreamMetadata(), null, maxWriteBatchLength: 50),
                new ProjectionVersion(1, 2, 2), new TransactionFilePositionTagger(0), CheckpointTag.FromPosition(0, 100, 50),
                _ioDispatcher, _readyHandler);
            _stream.Start();
            _stream.EmitEvents(CreateEventBatch());
            OneWriteCompletes();
        }

        [Test]
        public void truncates_existing_stream_at_correct_position()
        {
            var writes =
                HandledMessages.OfType<ClientMessage.WriteEvents>()
                    .OfEventType(SystemEventTypes.StreamMetadata)
                    .ToArray();
            Assert.AreEqual(1, writes.Length);
            HelperExtensions.AssertJson(new {___tb = 2}, writes[0].Data.ParseJson<JObject>());
        }

        [Test]
        public void publishes_all_events()
        {
            var writtenEvents =
                _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>()
                    .ExceptOfEventType(SystemEventTypes.StreamMetadata)
                    .ToArray();
            Assert.AreEqual(3, writtenEvents.Length);
            Assert.AreEqual("type1", writtenEvents[0].EventType);
            Assert.AreEqual("type2", writtenEvents[1].EventType);
            Assert.AreEqual("type3", writtenEvents[2].EventType);
        }

        [Test]
        public void updates_stream_metadata()
        {
            var writes =
                HandledMessages.OfType<ClientMessage.WriteEvents>()
                    .OfEventType(SystemEventTypes.StreamMetadata)
                    .ToArray();
            Assert.AreEqual(1, writes.Length);
        }

        [Test]
        public void reports_correct_event_numbers()
        {
            Assert.AreEqual(2, _1);
            Assert.AreEqual(3, _2);
            Assert.AreEqual(4, _3);
        }
    }
}
