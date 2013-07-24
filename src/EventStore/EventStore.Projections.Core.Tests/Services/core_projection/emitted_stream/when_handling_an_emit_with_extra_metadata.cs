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
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Core.Tests.Helpers;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace EventStore.Projections.Core.Tests.Services.core_projection.emitted_stream
{
    [TestFixture]
    public class when_handling_an_emit_with_extra_metadata : TestFixtureWithExistingEvents
    {
        private EmittedStream _stream;
        private TestCheckpointManagerMessageHandler _readyHandler;

        protected override void Given()
        {
            AllWritesQueueUp();
            ExistingEvent("test_stream", "type", @"{""c"": 100, ""p"": 50}", "data");
        }

        [SetUp]
        public void setup()
        {
            _readyHandler = new TestCheckpointManagerMessageHandler();
            _stream = new EmittedStream(
                "test_stream", new ProjectionVersion(1, 0, 0), null, new TransactionFilePositionTagger(), CheckpointTag.FromPosition(40, 30), _ioDispatcher,
                _readyHandler, maxWriteBatchLength: 50);
            _stream.Start();

            _stream.EmitEvents(
                new[]
                    {
                        new EmittedDataEvent(
                    "test_stream", Guid.NewGuid(), "type", "data",
                    new ExtraMetaData(new Dictionary<string, string> {{"a", "1"}, {"b", "{}"}}),
                    CheckpointTag.FromPosition(200, 150), null)
                    });

        }

        [Test]
        public void publishes_not_yet_published_events()
        {
            Assert.AreEqual(1, _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Count());
        }

        [Test]
        public void combines_checkpoint_tag_with_extra_metadata()
        {
            var writeEvent = _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Single();

            Assert.AreEqual(1, writeEvent.Events.Length);
            var @event = writeEvent.Events[0];
            var metadata = Helper.UTF8NoBom.GetString(@event.Metadata).ParseJson<JObject>();

            HelperExtensions.AssertJson(new {a = 1, b = new {}}, metadata);
            var checkpoint = @event.Metadata.ParseCheckpointTagJson();
            Assert.AreEqual(CheckpointTag.FromPosition(200, 150), checkpoint);
        }

    }
}
