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
    public class checkpoint_tag_by_phase
    {
        private readonly CheckpointTag _p0 = CheckpointTag.FromPosition(0, 1000, 9);
        private readonly CheckpointTag _p1a = CheckpointTag.FromPosition(1, 500, 400);
        private readonly CheckpointTag _p1b = CheckpointTag.FromPosition(1, 500, 450);
        private readonly CheckpointTag _p2 = CheckpointTag.FromPosition(2, 30, 29);
        private readonly CheckpointTag _p3 = CheckpointTag.FromStreamPosition(3, "stream", 100);

        private readonly CheckpointTag _p4 = CheckpointTag.FromEventTypeIndexPositions(
            4, new TFPos(200, 150), new Dictionary<string, int> {{"a", 1}});

        [Test]
        public void equal_equals()
        {
            Assert.IsTrue(_p0.Equals(_p0));
            Assert.IsTrue(_p1a.Equals(_p1a));
            Assert.IsTrue(_p1b.Equals(_p1b));
            Assert.IsTrue(_p2.Equals(_p2));
            Assert.IsTrue(_p3.Equals(_p3));
        }

        [Test]
        public void equal_operator()
        {
            Assert.IsTrue(_p1a == _p1a);
        }

        [Test]
        public void less_operator()
        {
            Assert.IsTrue(_p0 < _p1a);
            Assert.IsTrue(_p2 < _p3);
            Assert.IsTrue(_p3 < _p4);
        }

        [Test]
        public void less_or_equal_operator()
        {
            Assert.IsTrue(_p1b <= _p2);
            Assert.IsTrue(_p2 <= _p4);
            Assert.IsTrue(_p3 <= _p3);
        }

        [Test]
        public void greater_operator()
        {
            Assert.IsTrue(_p4 > _p1a);
            Assert.IsTrue(_p1b > _p1a);
        }

        [Test]
        public void greater_or_equal_operator()
        {
            Assert.IsTrue(_p1a >= _p0);
            Assert.IsTrue(_p4 >= _p3);
            Assert.IsTrue(_p3 >= _p1a);
            Assert.IsTrue(_p2 >= _p2);
        }
    }
#pragma warning restore 1718
}
