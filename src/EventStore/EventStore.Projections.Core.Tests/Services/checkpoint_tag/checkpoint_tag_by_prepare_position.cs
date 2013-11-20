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

#pragma warning disable 1718

using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.checkpoint_tag
{
    [TestFixture]
    public class checkpoint_tag_by_prepare_position
    {
        private readonly CheckpointTag _aa = CheckpointTag.FromPreparePosition(1, 9);
        private readonly CheckpointTag _b1 = CheckpointTag.FromPreparePosition(1, 15);
        private readonly CheckpointTag _b2 = CheckpointTag.FromPreparePosition(1, 15);
        private readonly CheckpointTag _cc = CheckpointTag.FromPreparePosition(1, 29);
        private readonly CheckpointTag _d1 = CheckpointTag.FromPreparePosition(1, 35);
        private readonly CheckpointTag _d2 = CheckpointTag.FromPreparePosition(1, 35);

        [Test]
        public void equal_equals()
        {
            Assert.IsTrue(_aa.Equals(_aa));
        }

        [Test]
        public void equal_operator()
        {
            Assert.IsTrue(_b1 == _b1);
            Assert.IsTrue(_b1 == _b2);
        }

        [Test]
        public void less_operator()
        {
            Assert.IsTrue(_aa < _b1);
        }

        [Test]
        public void less_or_equal_operator()
        {
            Assert.IsTrue(_aa <= _b1);
            Assert.IsTrue(_b1 <= _b2);
            Assert.IsTrue(_b2 <= _b2);
        }

        [Test]
        public void greater_operator()
        {
            Assert.IsTrue(_d1 > _cc);
            Assert.IsFalse(_d2 > _d1);
            Assert.IsFalse(_d2 > _d2);
        }

        [Test]
        public void greater_or_equal_operator()
        {
            Assert.IsTrue(_d1 >= _cc);
            Assert.IsTrue(_d2 >= _d1);
            Assert.IsTrue(_b2 >= _b2);
        }
    }
}
