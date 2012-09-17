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
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Core.Tests.Common;
using EventStore.Core.Tests.Fakes;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.emitted_stream
{
    [TestFixture]
    public class when_handling_an_emit_the_started_non_recovery_stream
    {
        private EmittedStream _stream;
        private FakePublisher _publisher;

        private TestMessageHandler<ProjectionMessage.Projections.ReadyForCheckpoint> _readyHandler;

        [SetUp]
        public void setup()
        {
            _publisher = new FakePublisher();
            _readyHandler = new TestMessageHandler<ProjectionMessage.Projections.ReadyForCheckpoint>();
            _stream = new EmittedStream("test", _publisher, _readyHandler, false, 50);
            _stream.Start();
            _stream.EmitEvents(
                new[] {new EmittedEvent("stream", Guid.NewGuid(), "type", "data")}, CheckpointTag.FromPosition(200, 150));
        }

        [Test]
        public void publishes_write_events()
        {
            Assert.IsTrue(_publisher.Messages.ContainsSingle<ClientMessage.WriteEvents>());
        }

        [Test]
        public void correct_stream_id_is_set_on_write_events_message()
        {
            Assert.AreEqual("test", _publisher.Messages.OfType<ClientMessage.WriteEvents>().Single().EventStreamId);
        }

        [Test]
        public void any_expected_version_is_set_on_write_events_message()
        {
            Assert.AreEqual(
                ExpectedVersion.Any, _publisher.Messages.OfType<ClientMessage.WriteEvents>().Single().ExpectedVersion);
        }

        [Test]
        public void metadata_include_commit_and_prepare_positions()
        {
            var metaData =
                _publisher.Messages.OfType<ClientMessage.WriteEvents>().Single().Events[0].Metadata.ParseJson
                    <CheckpointTag>();
            Assert.AreEqual(200, metaData.CommitPosition);
            Assert.AreEqual(150, metaData.PreparePosition);
        }
    }
}
