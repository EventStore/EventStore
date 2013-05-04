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
using EventStore.Core.Data;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.position_tagging.transaction_file_position_tagger
{
    [TestFixture]
    public class when_updating_transaction_file_postion_tracker
    {
        private PositionTagger _tagger;
        private PositionTracker _positionTracker;

        [SetUp]
        public void When()
        {
            // given
            _tagger = new TransactionFilePositionTagger();
            _positionTracker = new PositionTracker(_tagger);
            var newTag = CheckpointTag.FromPosition(100, 50);
            _positionTracker.UpdateByCheckpointTagInitial(newTag);
        }

        [Test]
        public void checkpoint_tag_is_for_correct_position()
        {
            Assert.AreEqual(new TFPos(100, 50), _positionTracker.LastTag.Position);
        }

        [Test, ExpectedException(typeof (InvalidOperationException))]
        public void cannot_update_to_the_same_postion()
        {
            var newTag = CheckpointTag.FromPosition(100, 50);
            _positionTracker.UpdateByCheckpointTagForward(newTag);
        }
    }
}
