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
using EventStore.Core.Data;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.position_tagging.event_by_type_index_position_tagger
{
    [TestFixture]
    public class when_updating_postion_event_by_type_index_position_tracker
    {
        private EventByTypeIndexPositionTagger _tagger;
        private PositionTracker _positionTracker;

        [SetUp]
        public void When()
        {
            // given
            _tagger = new EventByTypeIndexPositionTagger(0, new[] {"type1", "type2"});
            _positionTracker = new PositionTracker(_tagger);
            var newTag = CheckpointTag.FromEventTypeIndexPositions(0, new TFPos(10, 5), new Dictionary<string, int> {{"type1", 1}, {"type2", 2}});
            var newTag2 = CheckpointTag.FromEventTypeIndexPositions(0, new TFPos(20, 15), new Dictionary<string, int> {{"type1", 1}, {"type2", 3}});
            _positionTracker.UpdateByCheckpointTagInitial(newTag);
            _positionTracker.UpdateByCheckpointTagForward(newTag2);
        }

        [Test]
        public void stream_position_is_updated()
        {
            Assert.AreEqual(1, _positionTracker.LastTag.Streams["type1"]);
            Assert.AreEqual(3, _positionTracker.LastTag.Streams["type2"]);
        }

        [Test]
        public void tf_position_is_updated()
        {
            Assert.AreEqual(new TFPos(20, 15), _positionTracker.LastTag.Position);
        }
        
        [Test, ExpectedException(typeof (InvalidOperationException))]
        public void cannot_update_to_the_same_position()
        {
            var newTag = CheckpointTag.FromEventTypeIndexPositions(0, new TFPos(20, 15), new Dictionary<string, int> {{"type1", 1}, {"type2", 3}});
            _positionTracker.UpdateByCheckpointTagForward(newTag);
        }

        [Test]
        public void can_update_to_the_same_index_position_but_tf()
        {
            var newTag = CheckpointTag.FromEventTypeIndexPositions(0, new TFPos(30, 25), new Dictionary<string, int> {{"type1", 1}, {"type2", 3}});
            _positionTracker.UpdateByCheckpointTagForward(newTag);
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void it_cannot_be_updated_with_other_stream()
        {
            // even not initialized (UpdateToZero can be removed)
            var newTag = CheckpointTag.FromEventTypeIndexPositions(0, new TFPos(30, 25), new Dictionary<string, int> {{"type1", 1}, {"type3", 3}});
            _positionTracker.UpdateByCheckpointTagForward(newTag);
        }

        //TODO: write tests on updating with incompatible snapshot loaded
    }
}
