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
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.emitted_stream
{
    [TestFixture]
    public class when_handling_emits_with_previously_written_events : TestFixtureWithExistingEvents
    {
        private EmittedStream _stream;
        private TestCheckpointManagerMessageHandler _readyHandler;

        protected override void Given()
        {
            AllWritesQueueUp();
            ExistingEvent("test_stream", "type1", @"{""c"": 100, ""p"": 50}", "data");
            ExistingEvent("test_stream", "type2", @"{""c"": 200, ""p"": 150}", "data");
            ExistingEvent("test_stream", "type3", @"{""c"": 300, ""p"": 250}", "data");
        }

        [SetUp]
        public void setup()
        {
            _readyHandler = new TestCheckpointManagerMessageHandler();
            _stream = new EmittedStream(
                "test_stream", new EmittedStream.WriterConfiguration(new EmittedStream.WriterConfiguration.StreamMetadata(), null, maxWriteBatchLength: 50),
                new ProjectionVersion(1, 0, 0), new TransactionFilePositionTagger(0),
                CheckpointTag.FromPosition(0, 200, 150), _ioDispatcher, _readyHandler);
            _stream.Start();
        }

        [Test]
        public void does_not_publish_already_published_events()
        {
            _stream.EmitEvents(
                new[]
                {
                    new EmittedDataEvent(
                        "test_stream", Guid.NewGuid(), "type2", true, "data", null, CheckpointTag.FromPosition(0, 200, 150), null)
                });
            _stream.EmitEvents(
                new[]
                {
                    new EmittedDataEvent(
                        "test_stream", Guid.NewGuid(), "type3", true, "data", null, CheckpointTag.FromPosition(0, 300, 250), null)
                });
            Assert.AreEqual(0, _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Count());
        }

        [Test]
        public void publishes_not_yet_published_events()
        {
            _stream.EmitEvents(
                new[]
                {
                    new EmittedDataEvent(
                        "test_stream", Guid.NewGuid(), "type", true, "data", null, CheckpointTag.FromPosition(0, 400, 350), null)
                });
            Assert.AreEqual(1, _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Count());
        }

        [Test]
        public void replies_with_write_completed_message_for_existing_events()
        {
            _stream.EmitEvents(
                new[]
                {
                    new EmittedDataEvent(
                        "test_stream", Guid.NewGuid(), "type2", true, "data", null, CheckpointTag.FromPosition(0, 200, 150), null)
                });
            Assert.AreEqual(1, _readyHandler.HandledWriteCompletedMessage.Count);
        }

        [Test]
        public void retrieves_event_number_for_previously_written_events()
        {
            int eventNumber = -1;
            _stream.EmitEvents(
                new[]
                {
                    new EmittedDataEvent(
                        (string) "test_stream", Guid.NewGuid(), (string) "type2", (bool) true,
                        (string) "data", (ExtraMetaData) null, CheckpointTag.FromPosition(0, 200, 150), (CheckpointTag) null, v => eventNumber = v)
                });
            Assert.AreEqual(1, eventNumber);
        }

        [Test]
        public void reply_with_write_completed_message_when_write_completes()
        {
            _stream.EmitEvents(
                new[]
                {
                    new EmittedDataEvent(
                        "test_stream", Guid.NewGuid(), "type", true, "data", null, CheckpointTag.FromPosition(0, 400, 350), null)
                });
            OneWriteCompletes();
            Assert.IsTrue(_readyHandler.HandledWriteCompletedMessage.Any(v => v.StreamId == "test_stream"));
                // more than one is ok
        }

        [Test]
        public void reports_event_number_for_new_events()
        {
            int eventNumber = -1;
            _stream.EmitEvents(
                new[]
                {
                    new EmittedDataEvent(
                        (string) "test_stream", Guid.NewGuid(), (string) "type", (bool) true,
                        (string) "data", (ExtraMetaData) null, CheckpointTag.FromPosition(0, 400, 350), (CheckpointTag) null, v => eventNumber = v)
                });
            OneWriteCompletes();
            Assert.AreEqual(3, eventNumber);
        }
    }
}
