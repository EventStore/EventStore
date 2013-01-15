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
    public class when_relocking_the_state_at_later_position
    {
        private PartitionStateCache _cache;
        private CheckpointTag _cachedAtCheckpointTag;
        private PartitionStateCache.State _relockedData;

        [SetUp]
        public void given()
        {
            //given
            _cache = new PartitionStateCache(CheckpointTag.FromPosition(0, -1));
            _cachedAtCheckpointTag = CheckpointTag.FromPosition(1000, 900);
            _cache.CacheAndLockPartitionState("partition", new PartitionStateCache.State("data", _cachedAtCheckpointTag), _cachedAtCheckpointTag);
            _relockedData = _cache.TryGetAndLockPartitionState("partition", CheckpointTag.FromPosition(2000, 1900));
        }

        [Test]
        public void returns_correct_cached_data()
        {
            Assert.AreEqual("data", _relockedData.Data);
        }

        [Test]
        public void relocked_state_can_be_retrieved_as_locked()
        {
            var state = _cache.GetLockedPartitionState("partition");
            Assert.AreEqual("data", state.Data);
        }

        [Test, ExpectedException(typeof (InvalidOperationException))]
        public void cannot_be_relocked_at_the_previous_position()
        {
            _cache.TryGetAndLockPartitionState("partition", _cachedAtCheckpointTag);
        }

        [Test]
        public void the_state_can_be_retrieved()
        {
            var state = _cache.TryGetPartitionState("partition");
            Assert.AreEqual("data", state.Data);
        }

    }
}
