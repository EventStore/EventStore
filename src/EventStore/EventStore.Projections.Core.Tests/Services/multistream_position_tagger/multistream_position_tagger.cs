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
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.multistream_position_tagger
{
    [TestFixture]
    public class multistream_position_tagger
    {
        [Test]
        public void can_be_created()
        {
            var t = new MultiStreamPositionTagger(new[] {"stream1", "stream2"});
            var tr = new PositionTracker(t);
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_streams_throws_argument_null_exception()
        {
            var t = new MultiStreamPositionTagger(null);
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void empty_streams_throws_argument_exception()
        {
            var t = new MultiStreamPositionTagger(new string[] {});
        }

        [Test]
        public void position_checkpoint_tag_is_incompatible()
        {
            var t = new MultiStreamPositionTagger(new[] {"stream1", "stream2"});
            Assert.IsFalse(t.IsCompatible(CheckpointTag.FromPosition(1000, 500)));
        }

        [Test]
        public void another_streams_checkpoint_tag_is_incompatible()
        {
            var t = new MultiStreamPositionTagger(new[] {"stream1", "stream2"});
            Assert.IsFalse(
                t.IsCompatible(
                    CheckpointTag.FromStreamPositions(
                        new Dictionary<string, int> {{"stream2", 100}, {"stream3", 150}}, 200)));
        }

        [Test]
        public void the_same_stream_checkpoint_tag_is_compatible()
        {
            var t = new MultiStreamPositionTagger(new[] {"stream1", "stream2"});
            Assert.IsTrue(
                t.IsCompatible(
                    CheckpointTag.FromStreamPositions(
                        new Dictionary<string, int> {{"stream1", 100}, {"stream2", 150}}, 200)));
        }
    }
}
