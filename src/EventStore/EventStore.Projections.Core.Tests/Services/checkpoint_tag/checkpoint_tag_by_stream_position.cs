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

#pragma warning disable 1718 // allow a == a comparison

namespace EventStore.Projections.Core.Tests.Services.checkpoint_tag
{
    [TestFixture]
    public class checkpoint_tag_by_stream_position
    {
        private readonly CheckpointTag _a = CheckpointTag.FromStreamPosition("stream", 9);
        private readonly CheckpointTag _b = CheckpointTag.FromStreamPosition("stream", 15);
        private readonly CheckpointTag _c = CheckpointTag.FromStreamPosition("stream", 29);

        [Test]
        public void equal_equals()
        {
            Assert.IsTrue(_a.Equals(_a));
        }

        [Test]
        public void equal_operator()
        {
            Assert.IsTrue(_b == _b);
        }

        [Test]
        public void less_operator()
        {
            Assert.IsTrue(_a < _b);
        }

        [Test]
        public void less_or_equal_operator()
        {
            Assert.IsTrue(_a <= _b);
            Assert.IsTrue(_c <= _c);
        }

        [Test]
        public void greater_operator()
        {
            Assert.IsTrue(_b > _a);
        }

        [Test]
        public void greater_or_equal_operator()
        {
            Assert.IsTrue(_b >= _a);
            Assert.IsTrue(_c >= _c);
        }
    }
#pragma warning restore 1718
}
