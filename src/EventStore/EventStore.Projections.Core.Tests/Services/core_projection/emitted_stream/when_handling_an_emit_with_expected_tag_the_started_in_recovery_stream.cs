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
using EventStore.Core.Util;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.emitted_stream
{
    [TestFixture]
    public class when_handling_an_emit_with_expected_tag_the_started_in_recovery_stream : TestFixtureWithExistingEvents
    {
        private EmittedStream _stream;
        private TestCheckpointManagerMessageHandler _readyHandler;

        protected override void Given()
        {
            ExistingEvent("test_stream", "type", @"{""c"": 100, ""p"": 50}", "data");
        }

        [SetUp]
        public void setup()
        {
            _readyHandler = new TestCheckpointManagerMessageHandler();
            _stream = new EmittedStream(
                "test_stream", new ProjectionVersion(1, 0, 0), null, new TransactionFilePositionTagger(), CheckpointTag.FromPosition(0, -1), _ioDispatcher,
                _readyHandler, maxWriteBatchLength: 50);
            _stream.Start();
        }

        [Test]
        public void does_not_publish_already_published_events()
        {
            _stream.EmitEvents(
                new[]
                    {
                        new EmittedDataEvent(
                    "test_stream", Guid.NewGuid(), "type", "data", null, CheckpointTag.FromPosition(100, 50),
                    CheckpointTag.FromPosition(40, 20))
                    });
            Assert.AreEqual(0, _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Count());
        }

        [Test]
        public void publishes_not_yet_published_events_if_expected_tag_is_the_same()
        {
            _stream.EmitEvents(
                new[]
                    {
                        new EmittedDataEvent(
                    "test_stream", Guid.NewGuid(), "type", "data", null, CheckpointTag.FromPosition(200, 150),
                    CheckpointTag.FromPosition(100, 50))
                    });
            Assert.AreEqual(1, _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Count());
        }

        [Test]
        public void does_not_publish_not_yet_published_events_if_expected_tag_is_before_last_event_tag()
        {
            //TODO: is it corrupted dB case? 
            _stream.EmitEvents(
                new[]
                    {
                        new EmittedDataEvent(
                    "test_stream", Guid.NewGuid(), "type", "data", null, CheckpointTag.FromPosition(200, 150),
                    CheckpointTag.FromPosition(40, 20))
                    });
            Assert.AreEqual(0, _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Count());
        }

        [Test]
        public void correct_stream_id_is_set_on_write_events_message()
        {
            _stream.EmitEvents(
                new[]
                    {
                        new EmittedDataEvent(
                    "test_stream", Guid.NewGuid(), "type", "data", null, CheckpointTag.FromPosition(200, 150),
                    CheckpointTag.FromPosition(100, 50))
                    });
            Assert.AreEqual("test_stream", _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Single().EventStreamId);
        }

        [Test]
        public void metadata_include_commit_and_prepare_positions()
        {
            _stream.EmitEvents(
                new[]
                    {
                        new EmittedDataEvent(
                    "test_stream", Guid.NewGuid(), "type", "data", null, CheckpointTag.FromPosition(200, 150),
                    CheckpointTag.FromPosition(100, 50))
                    });
            var metaData =
                _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Single().Events[0].Metadata.ParseCheckpointTagVersionExtraJson(default(ProjectionVersion));
            Assert.AreEqual(200, metaData.Tag.CommitPosition);
            Assert.AreEqual(150, metaData.Tag.PreparePosition);
        }

    }
}
