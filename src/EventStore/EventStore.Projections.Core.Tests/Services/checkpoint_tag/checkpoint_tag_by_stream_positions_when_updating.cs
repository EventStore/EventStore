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
using System.Linq;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.checkpoint_tag
{
    [TestFixture]
    public class checkpoint_tag_by_stream_positions_when_updating
    {
        private readonly CheckpointTag _a1 = CheckpointTag.FromStreamPositions(new Dictionary<string, int> {{"a", 1}});

        private readonly CheckpointTag _b1 = CheckpointTag.FromStreamPositions(new Dictionary<string, int> {{"b", 1}});

        private readonly CheckpointTag _a1b1 =
            CheckpointTag.FromStreamPositions(new Dictionary<string, int> {{"a", 1}, {"b", 1}});

        private readonly CheckpointTag _a2b1 =
            CheckpointTag.FromStreamPositions(new Dictionary<string, int> {{"a", 2}, {"b", 1}});

        private readonly CheckpointTag _a1b2 =
            CheckpointTag.FromStreamPositions(new Dictionary<string, int> {{"a", 1}, {"b", 2}});

        private readonly CheckpointTag _a2b2 =
            CheckpointTag.FromStreamPositions(new Dictionary<string, int> {{"a", 2}, {"b", 2}});

        [Test]
        public void updated_position_is_correct()
        {
            var updated = _a1b1.UpdateStreamPosition("a", 2);
            Assert.AreEqual(2, updated.Streams["a"]);
        }

        [Test]
        public void other_stream_position_is_correct()
        {
            var updated = _a1b1.UpdateStreamPosition("a", 2);
            Assert.AreEqual(1, updated.Streams["b"]);
        }

        [Test]
        public void streams_are_correct()
        {
            var updated = _a1b1.UpdateStreamPosition("a", 2);
            Assert.AreEqual(2, updated.Streams.Count);
            Assert.IsTrue(
                updated.Streams.Any(v => v.Key == "a"));
            Assert.IsTrue(
                updated.Streams.Any(v => v.Key == "b"));
        }

    }
}
