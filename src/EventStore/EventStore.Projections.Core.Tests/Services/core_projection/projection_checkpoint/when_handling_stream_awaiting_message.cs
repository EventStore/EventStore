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

using EventStore.Core.Tests.Services.Replication;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.projection_checkpoint
{
    [TestFixture]
    public class when_handling_stream_awaiting_message : TestFixtureWithExistingEvents
    {
        private ProjectionCheckpoint _checkpoint;
        private TestCheckpointManagerMessageHandler _readyHandler;
        private FakeEnvelope _fakeEnvelope;

        [SetUp]
        public void setup()
        {
            _readyHandler = new TestCheckpointManagerMessageHandler();
            _checkpoint = new ProjectionCheckpoint(
                _readDispatcher, _writeDispatcher, _readyHandler, CheckpointTag.FromPosition(100, 50),
                CheckpointTag.FromPosition(0, -1), 250);

            _fakeEnvelope = new FakeEnvelope();
            _checkpoint.Handle(new CoreProjectionProcessingMessage.EmittedStreamAwaiting("awaiting_stream", _fakeEnvelope));
        }

        [Test]
        public void broadcasts_write_completed_to_awaiting_streams()
        {
            _checkpoint.Handle(new CoreProjectionProcessingMessage.EmittedStreamWriteCompleted("completed_stream"));
            Assert.AreEqual(1, _fakeEnvelope.Replies.Count);
            Assert.IsInstanceOf<CoreProjectionProcessingMessage.EmittedStreamWriteCompleted>(_fakeEnvelope.Replies[0]);
        }

        [Test]
        public void does_not_broadcast_second_write_completed_to_awaiting_streams()
        {
            _checkpoint.Handle(new CoreProjectionProcessingMessage.EmittedStreamWriteCompleted("completed_stream1"));
            _checkpoint.Handle(new CoreProjectionProcessingMessage.EmittedStreamWriteCompleted("completed_stream2"));
            Assert.AreEqual(1, _fakeEnvelope.Replies.Count);
            Assert.IsInstanceOf<CoreProjectionProcessingMessage.EmittedStreamWriteCompleted>(_fakeEnvelope.Replies[0]);
            Assert.AreEqual("completed_stream1", ((CoreProjectionProcessingMessage.EmittedStreamWriteCompleted)_fakeEnvelope.Replies[0]).StreamId);
        }
    }
}
