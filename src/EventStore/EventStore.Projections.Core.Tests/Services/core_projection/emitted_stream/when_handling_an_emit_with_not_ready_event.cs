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
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.emitted_stream
{
    [TestFixture]
    public class when_handling_an_emit_with_not_ready_event : TestFixtureWithExistingEvents
    {
        private EmittedStream _stream;
        private TestCheckpointManagerMessageHandler _readyHandler;

        protected override void Given()
        {
            NoStream("test_stream");
            AllWritesSucceed();
        }

        [SetUp]
        public void setup()
        {
            _readyHandler = new TestCheckpointManagerMessageHandler();
            _stream = new EmittedStream(
                "test_stream", CheckpointTag.FromPosition(0, -1), CheckpointTag.FromPosition(0, -1), _readDispatcher,
                _writeDispatcher, _readyHandler, maxWriteBatchLength: 50);
            _stream.Start();
        }

        [Test]
        public void replies_with_await_message()
        {
            _stream.EmitEvents(
                new[]
                    {
                        new EmittedLinkTo(
                    "test_stream", Guid.NewGuid(), "other_stream", CheckpointTag.FromPosition(1100, 1000), null)
                    });
            Assert.AreEqual(1, _readyHandler.HandledStreamAwaitingMessage.Count);
            Assert.AreEqual("test_stream", _readyHandler.HandledStreamAwaitingMessage[0].StreamId);
        }

        [Test]
        public void processes_write_on_write_completed_if_ready()
        {
            var linkTo = new EmittedLinkTo("test_stream", Guid.NewGuid(), "other_stream", CheckpointTag.FromPosition(1100, 1000), null);
            _stream.EmitEvents(
                new[]
                    {
                        linkTo
                    });
            linkTo.SetTargetEventNumber(1);
            _stream.Handle(new CoreProjectionProcessingMessage.EmittedStreamWriteCompleted("other_stream"));

            Assert.AreEqual(1, _readyHandler.HandledStreamAwaitingMessage.Count);
            Assert.AreEqual(1, _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Count());
        }

        [Test]
        public void replies_with_await_message_on_write_completed_if_not_yet_ready()
        {
            var linkTo = new EmittedLinkTo("test_stream", Guid.NewGuid(), "other_stream", CheckpointTag.FromPosition(1100, 1000), null);
            _stream.EmitEvents(
                new[]
                    {
                        linkTo
                    });
            _stream.Handle(new CoreProjectionProcessingMessage.EmittedStreamWriteCompleted("one_more_stream"));

            Assert.AreEqual(2, _readyHandler.HandledStreamAwaitingMessage.Count);
            Assert.AreEqual("test_stream", _readyHandler.HandledStreamAwaitingMessage[0].StreamId);
            Assert.AreEqual("test_stream", _readyHandler.HandledStreamAwaitingMessage[1].StreamId);
        }

    }
}
