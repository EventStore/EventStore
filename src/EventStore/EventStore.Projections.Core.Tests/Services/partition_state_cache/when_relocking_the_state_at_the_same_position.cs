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

namespace EventStore.Projections.Core.Tests.Services.partition_state_cache
{
    [TestFixture]
    public class when_relocking_the_state_at_the_same_position
    {
        private PartitionStateCache _cache;
        private CheckpointTag _cachedAtCheckpointTag;

        [SetUp]
        public void given()
        {
            //given
            _cache = new PartitionStateCache();
            _cachedAtCheckpointTag = CheckpointTag.FromPosition(0, 1000, 900);
            _cache.CacheAndLockPartitionState("partition", new PartitionState("data", null, _cachedAtCheckpointTag), _cachedAtCheckpointTag);
        }

        [Test, ExpectedException(typeof (InvalidOperationException))]
        public void thorws_invalid_operation_exception_if_not_allowed()
        {
            _cache.TryGetAndLockPartitionState("partition", CheckpointTag.FromPosition(0, 1000, 900));
        }

        [Test]
        public void the_state_can_be_retrieved()
        {
            var state = _cache.TryGetPartitionState("partition");
            Assert.AreEqual("data", state.State);
        }

    }
}
