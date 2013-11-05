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

namespace EventStore.Projections.Core.Tests.Services
{
    [TestFixture]
    public class mixed_checkpoint_tags
    {
        private readonly CheckpointTag _a = CheckpointTag.FromStreamPosition(0, "stream1", 9);
        private readonly CheckpointTag _b = CheckpointTag.FromStreamPosition(0, "stream2", 15);
        private readonly CheckpointTag _c = CheckpointTag.FromPosition(0, 50, 29);

        [Test]
        public void are_not_equal()
        {
            Assert.AreNotEqual(_a, _b);
            Assert.AreNotEqual(_a, _c);
            Assert.AreNotEqual(_b, _c);

            Assert.IsTrue(_a != _b);
            Assert.IsTrue(_a != _c);
            Assert.IsTrue(_b != _c);
        }

        [Test]
        public void cannot_be_compared()
        {
            Assert.IsTrue(throws(() => _a > _b));
            Assert.IsTrue(throws(() => _a >= _b));
            Assert.IsTrue(throws(() => _a > _c));
            Assert.IsTrue(throws(() => _a >= _c));
            Assert.IsTrue(throws(() => _b > _c));
            Assert.IsTrue(throws(() => _b >= _c));
            Assert.IsTrue(throws(() => _a < _b));
            Assert.IsTrue(throws(() => _a <= _b));
            Assert.IsTrue(throws(() => _a < _c));
            Assert.IsTrue(throws(() => _a <= _c));
            Assert.IsTrue(throws(() => _b < _c));
            Assert.IsTrue(throws(() => _b <= _c));
        }

        private bool throws(Func<bool> func)
        {
            try
            {
                func();
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}
