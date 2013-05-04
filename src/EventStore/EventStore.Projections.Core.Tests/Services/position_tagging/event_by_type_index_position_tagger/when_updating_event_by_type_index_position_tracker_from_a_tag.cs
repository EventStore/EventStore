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

using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.position_tagging.event_by_type_index_position_tagger
{
    [TestFixture]
    public class when_updating_event_by_type_index_position_tracker_from_a_tag
    {
        private EventByTypeIndexPositionTagger _tagger;
        private CheckpointTag _tag;
        private PositionTracker _positionTracker;

        [SetUp]
        public void When()
        {
            // given
            var tagger = new EventByTypeIndexPositionTagger(new[] {"type1", "type2"});
            var tracker = new PositionTracker(tagger);

            var newTag = CheckpointTag.FromEventTypeIndexPositions(
                new TFPos(10, 5), new Dictionary<string, int> {{"type1", 1}, {"type2", 2}});

            tracker.UpdateByCheckpointTagInitial(newTag);
            _tag = tracker.LastTag;
            _tagger = new EventByTypeIndexPositionTagger(new[] {"type1", "type2"});
            _positionTracker = new PositionTracker(_tagger);
            // when 

            _positionTracker.UpdateByCheckpointTagInitial(_tag);
        }

        [Test]
        public void stream_position_is_updated()
        {
            Assert.AreEqual(1, _positionTracker.LastTag.Streams["type1"]);
            Assert.AreEqual(2, _positionTracker.LastTag.Streams["type2"]);
        }

        [Test]
        public void tf_stream_position_is_updated()
        {
            Assert.AreEqual(new TFPos(10, 5), _positionTracker.LastTag.Position);
        }

    }
}
