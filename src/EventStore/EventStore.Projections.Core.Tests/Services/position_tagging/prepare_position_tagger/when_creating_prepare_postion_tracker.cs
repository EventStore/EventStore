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

namespace EventStore.Projections.Core.Tests.Services.position_tagging.prepare_position_tagger
{
    [TestFixture]
    public class when_creating_prepare_postion_tracker
    {
        private PositionTagger _tagger;
        private PositionTracker _positionTracker;

        [SetUp]
        public void when()
        {
            _tagger = new PreparePositionTagger();
            _positionTracker = new PositionTracker(_tagger);
        }

        [Test]
        public void it_can_be_updated()
        {
            // even not initialized (UpdateToZero can be removed)
            var newTag = CheckpointTag.FromPreparePosition(50);
            _positionTracker.UpdateByCheckpointTagInitial(newTag);
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void initial_position_cannot_be_set_twice()
        {
            var newTag = CheckpointTag.FromPreparePosition(50);
            _positionTracker.UpdateByCheckpointTagForward(newTag);
            _positionTracker.UpdateByCheckpointTagForward(newTag);
        }

        [Test]
        public void it_can_be_updated_to_zero()
        {
            _positionTracker.UpdateByCheckpointTagInitial(_tagger.MakeZeroCheckpointTag());
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void it_cannot_be_updated_forward()
        {
            var newTag = CheckpointTag.FromPreparePosition(50);
            _positionTracker.UpdateByCheckpointTagForward(newTag);
        }

    }
}
