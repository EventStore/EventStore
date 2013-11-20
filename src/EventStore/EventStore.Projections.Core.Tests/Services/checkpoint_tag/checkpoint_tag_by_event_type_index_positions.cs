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

#pragma warning disable 1718 // allow a == a comparison

namespace EventStore.Projections.Core.Tests.Services.checkpoint_tag
{
    [TestFixture]
    public class checkpoint_tag_by_event_type_index_positions
    {
        private readonly CheckpointTag _a1 = CheckpointTag.FromEventTypeIndexPositions(
            1, new TFPos(100, 50), new Dictionary<string, int> {{"a", 1}});

        private readonly CheckpointTag _a1_prime = CheckpointTag.FromEventTypeIndexPositions(
            1, new TFPos(100, 50), new Dictionary<string, int> {{"a", 0}});

        private readonly CheckpointTag _b1 = CheckpointTag.FromEventTypeIndexPositions(
            1, new TFPos(200, 150), new Dictionary<string, int> {{"b", 1}});

        private readonly CheckpointTag _a1b1 = CheckpointTag.FromEventTypeIndexPositions(
            1, new TFPos(300, 250), new Dictionary<string, int> {{"a", 1}, {"b", 1}});

        private readonly CheckpointTag _a2b1 = CheckpointTag.FromEventTypeIndexPositions(
            1, new TFPos(400, 350), new Dictionary<string, int> {{"a", 2}, {"b", 1}});

        private readonly CheckpointTag _a1b2 = CheckpointTag.FromEventTypeIndexPositions(
            1, new TFPos(500, 450), new Dictionary<string, int> {{"a", 1}, {"b", 2}});

        private readonly CheckpointTag _a2b2 = CheckpointTag.FromEventTypeIndexPositions(
            1, new TFPos(600, 550), new Dictionary<string, int> {{"a", 2}, {"b", 2}});

        [Test]
        public void equal_equals()
        {
            Assert.IsTrue(_a1.Equals(_a1));
        }

        [Test]
        public void equal_but_different_index_positions_still_equals()
        {
            Assert.IsTrue(_a1.Equals(_a1_prime));
        }

        [Test]
        public void equal_operator()
        {
            Assert.IsTrue(_a2b1 == _a2b1);
        }

        [Test]
        public void less_operator()
        {
            Assert.IsTrue(_a1 < _a1b1);
            Assert.IsTrue(_a1 < _a1b2);
            Assert.IsTrue(_a1 < _a2b2);
            Assert.IsFalse(_a1b2 < _a1b2);
            Assert.IsFalse(_a1b2 < _a1b1);
            Assert.IsFalse(_a1b2 < _a2b1);
            Assert.IsTrue(_a1 < _b1);
        }

        [Test]
        public void less_or_equal_operator()
        {
            Assert.IsTrue(_a1 <= _a1b1);
            Assert.IsTrue(_a1 <= _a1b2);
            Assert.IsTrue(_a1 <= _a2b2);
            Assert.IsTrue(_a1b2 <= _a1b2);
            Assert.IsFalse(_a1b2 <= _a1b1);
            Assert.IsFalse(_a1b2 <= _a2b1);
            Assert.IsTrue(_a1 <= _b1);
        }

        [Test]
        public void greater_operator()
        {
            Assert.IsFalse(_a1 > _a1b1);
            Assert.IsFalse(_a1 > _a1b2);
            Assert.IsFalse(_a1 > _a2b2);
            Assert.IsFalse(_a1b2 > _a1b2);
            Assert.IsTrue(_a1b2 > _a1b1);
            Assert.IsTrue(_a1b2 > _a2b1);
            Assert.IsFalse(_a1 > _b1);
        }

        [Test]
        public void greater_or_equal_operator()
        {
            Assert.IsFalse(_a1 >= _a1b1);
            Assert.IsFalse(_a1 >= _a1b2);
            Assert.IsFalse(_a1 >= _a2b2);
            Assert.IsTrue(_a1b2 >= _a1b2);
            Assert.IsTrue(_a1b2 >= _a1b1);
            Assert.IsTrue(_a1b2 >= _a2b1);
            Assert.IsFalse(_a1 >= _b1);
        }
    }
#pragma warning restore 1718
}
