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
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.position_tagging.stream_position_tagger
{
    [TestFixture]
    public class when_updating_postion_stream_position_tracker
    {
        private StreamPositionTagger _tagger;
        private PositionTracker _positionTracker;

        [SetUp]
        public void When()
        {
            // given
            _tagger = new StreamPositionTagger("stream1");
            _positionTracker = new PositionTracker(_tagger);
            var newTag = CheckpointTag.FromStreamPosition("stream1", 1);
            var newTag2 = CheckpointTag.FromStreamPosition("stream1", 2);
            _positionTracker.UpdateByCheckpointTagInitial(newTag);
            _positionTracker.UpdateByCheckpointTagForward(newTag2);
        }

        [Test]
        public void stream_position_is_updated()
        {
            Assert.AreEqual(2, _positionTracker.LastTag.Streams["stream1"]);
        }

        
        [Test, ExpectedException(typeof (InvalidOperationException))]
        public void cannot_update_to_the_same_postion()
        {
            var newTag = CheckpointTag.FromStreamPosition("stream1", 2);
            _positionTracker.UpdateByCheckpointTagForward(newTag);
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void it_cannot_be_updated_with_other_stream()
        {
            // even not initialized (UpdateToZero can be removed)
            var newTag = CheckpointTag.FromStreamPosition("other_stream1", 2);
            _positionTracker.UpdateByCheckpointTagForward(newTag);
        }

        //TODO: write tests on updating with incompatible snapshot loaded
    }
}
