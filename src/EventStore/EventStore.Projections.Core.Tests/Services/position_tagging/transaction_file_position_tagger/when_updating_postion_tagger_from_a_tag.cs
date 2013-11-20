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

using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.position_tagging.transaction_file_position_tagger
{
    [TestFixture]
    public class when_updating_postion_tagger_from_a_tag
    {
        private PositionTagger _tagger;
        private CheckpointTag _tag;
        private PositionTracker _positionTracker;

        [SetUp]
        public void When()
        {
            // given
            var tagger = new TransactionFilePositionTagger(0);
            var positionTracker = new PositionTracker(tagger);

            var newTag = CheckpointTag.FromPosition(0, 100, 50);
            positionTracker.UpdateByCheckpointTagInitial(newTag);
            _tag = positionTracker.LastTag;
            _tagger = new TransactionFilePositionTagger(0);
            _positionTracker = new PositionTracker(_tagger);
            // when 

            _positionTracker.UpdateByCheckpointTagInitial(_tag);
        }

        [Test]
        public void position_is_updated()
        {
            Assert.AreEqual(50, _positionTracker.LastTag.PreparePosition);
            Assert.AreEqual(100, _positionTracker.LastTag.CommitPosition);
        }
    }
}
