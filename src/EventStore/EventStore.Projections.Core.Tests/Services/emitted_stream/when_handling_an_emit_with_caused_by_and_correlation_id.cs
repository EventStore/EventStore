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

namespace EventStore.Projections.Core.Tests.Services.emitted_stream
{
    [TestFixture]
    public class when_handling_an_emit_with_caused_by_and_correlation_id : TestFixtureWithExistingEvents
    {
        private EmittedStream _stream;
        private TestCheckpointManagerMessageHandler _readyHandler;
        private EmittedDataEvent _emittedDataEvent;
        private Guid _causedBy;
        private string _correlationId;

        protected override void Given()
        {
            AllWritesQueueUp();
            AllWritesToSucceed("$$test_stream");
            NoOtherStreams();
        }

        [SetUp]
        public void setup()
        {
            _causedBy = Guid.NewGuid();
            _correlationId = "correlation_id";

            _emittedDataEvent = new EmittedDataEvent(
                "test_stream", Guid.NewGuid(), "type", true, "data", null, CheckpointTag.FromPosition(0, 200, 150), null);

            _emittedDataEvent.SetCausedBy(_causedBy);
            _emittedDataEvent.SetCorrelationId(_correlationId);

            _readyHandler = new TestCheckpointManagerMessageHandler();
            _stream = new EmittedStream(
                "test_stream", new EmittedStream.WriterConfiguration(new EmittedStream.WriterConfiguration.StreamMetadata(), null, maxWriteBatchLength: 50),
                new ProjectionVersion(1, 0, 0), new TransactionFilePositionTagger(0), CheckpointTag.FromPosition(0, 40, 30),
                _ioDispatcher, _readyHandler);
            _stream.Start();
            _stream.EmitEvents(new[] {_emittedDataEvent});
        }


        [Test]
        public void publishes_write_events()
        {
            var writeEvents =
                _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>()
                    .ExceptOfEventType(SystemEventTypes.StreamMetadata)
                    .ToArray();
            Assert.AreEqual(1, writeEvents.Length);
            var writeEvent = writeEvents.Single();
            Assert.NotNull(writeEvent.Metadata);
            var metadata = Helper.UTF8NoBom.GetString(writeEvent.Metadata);
            HelperExtensions.AssertJson(
                new {___causedBy = _causedBy, ___correlationId = _correlationId}, metadata.ParseJson<JObject>());
        }
    }
}
