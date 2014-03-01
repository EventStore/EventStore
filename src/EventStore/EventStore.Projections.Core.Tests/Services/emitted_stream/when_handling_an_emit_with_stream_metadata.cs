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
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.emitted_stream
{
    [TestFixture]
    public class when_handling_an_emit_with_stream_metadata_to_empty_stream : TestFixtureWithExistingEvents
    {
        private EmittedStream _stream;
        private TestCheckpointManagerMessageHandler _readyHandler;
        private EmittedStream.WriterConfiguration.StreamMetadata _streamMetadata;
        private EmittedStream.WriterConfiguration _writerConfiguration;

        protected override void Given()
        {
            AllWritesQueueUp();
            NoStream("test_stream");
            _streamMetadata = new EmittedStream.WriterConfiguration.StreamMetadata(maxCount: 10);
            _writerConfiguration = new EmittedStream.WriterConfiguration(_streamMetadata, null, maxWriteBatchLength: 50);
        }

        [SetUp]
        public void setup()
        {
            _readyHandler = new TestCheckpointManagerMessageHandler();
            _stream = new EmittedStream(
                "test_stream", _writerConfiguration, new ProjectionVersion(1, 0, 0), new TransactionFilePositionTagger(0),
                CheckpointTag.FromPosition(0, 40, 30), _ioDispatcher, _readyHandler);
            _stream.Start();
            _stream.EmitEvents(
                new[]
                {
                    new EmittedDataEvent(
                        "test_stream", Guid.NewGuid(), "type", true, "data", null, CheckpointTag.FromPosition(0, 200, 150), null)
                });
        }

        [Test]
        public void publishes_write_stream_metadata()
        {
            Assert.AreEqual(
                1, _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().ToStream("$$test_stream").Count());
        }

        [Test]
        public void does_not_write_stream_metadata_second_time()
        {
            OneWriteCompletes();
            OneWriteCompletes();
            _stream.EmitEvents(
                new[]
                {
                    new EmittedDataEvent(
                        "test_stream", Guid.NewGuid(), "type", true, "data", null, CheckpointTag.FromPosition(0, 400, 350), null)
                });
            Assert.AreEqual(
                1, _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().ToStream("$$test_stream").Count());
        }

        [Test]
        public void publishes_write_emitted_event_on_write_stream_metadata_completed()
        {
            OneWriteCompletes();
            Assert.AreEqual(
                1, _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().ToStream("test_stream").Count());
        }

        [Test]
        public void does_not_reply_with_write_completed_message()
        {
            Assert.AreEqual(0, _readyHandler.HandledWriteCompletedMessage.Count);
        }

        [Test]
        public void reply_with_write_completed_message_when_write_completes()
        {
            OneWriteCompletes();
            OneWriteCompletes();
            Assert.IsTrue(_readyHandler.HandledWriteCompletedMessage.Any(v => v.StreamId == "test_stream"));
            // more than one is ok
        }
    }
}
