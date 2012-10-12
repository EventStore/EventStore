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
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Infrastructure.Services.Storage
{
    [TestFixture]
    public class when_caching_read_items
    {
        private readonly Guid _id = Guid.NewGuid();

        [Test]
        public void the_item_can_be_read()
        {
            var cache = new DictionaryBasedCache();
            cache.PutRecord(12000, new PrepareLogRecord(12000, _id, _id, 12000, 0, "test", 1, DateTime.UtcNow, 
                                                        PrepareFlags.None, "type", new byte[0], new byte[0]));
            PrepareLogRecord read;
            Assert.IsTrue(cache.TryGetRecord(12000, out read));
            Assert.AreEqual(_id, read.EventId);
        }

        [Test]
        public void cache_removes_oldest_item_when_max_count_reached()
        {
            var cache = new DictionaryBasedCache(9, 1024*1024*16);
            for (int i = 0; i < 10;i++ )
                cache.PutRecord(i, new PrepareLogRecord(0, Guid.NewGuid(), _id, 0, 0, "test", 1, DateTime.UtcNow, 
                                                        PrepareFlags.None, "type", new byte[0], new byte[0]));
            PrepareLogRecord read;
            Assert.IsFalse(cache.TryGetRecord(0, out read));
        }

        [Test]
        public void cache_removes_oldest_item_when_max_size_reached_by_data()
        {
            var cache = new DictionaryBasedCache(100, 1024 * 9);
            for (int i = 0; i < 10; i++)
                cache.PutRecord(i, new PrepareLogRecord(0, Guid.NewGuid(), _id, 0, 0, "test", 1, DateTime.UtcNow, 
                                                        PrepareFlags.None, "type", new byte[1024], new byte[0]));
            PrepareLogRecord read;
            Assert.IsFalse(cache.TryGetRecord(0, out read));
        }

        [Test]
        public void cache_removes_oldest_item_when_max_size_reached_metadata()
        {
            var cache = new DictionaryBasedCache(100, 1024 * 9);
            for (int i = 0; i < 10; i++)
                cache.PutRecord(i, new PrepareLogRecord(0, Guid.NewGuid(), _id, 0, 0, "test", 1, DateTime.UtcNow, 
                                                        PrepareFlags.None, "type", new byte[0], new byte[1024]));
            PrepareLogRecord read;
            Assert.IsFalse(cache.TryGetRecord(0, out read));
        }

        [Test]
        public void empty_cache_has_zeroed_statistics()
        {
            var cache = new DictionaryBasedCache(100, 1024 * 9);
            var stats = cache.GetStatistics();
            Assert.AreEqual(0, stats.MissCount);
            Assert.AreEqual(0, stats.HitCount);
            Assert.AreEqual(0, stats.Size);
            Assert.AreEqual(0, stats.Count);
        }

        [Test]
        public void statistics_are_updated_with_hits()
        {
            var cache = new DictionaryBasedCache(100, 1024 * 9);
            cache.PutRecord(1, new PrepareLogRecord(1, Guid.NewGuid(), _id, 1, 0, "test", 1, DateTime.UtcNow, 
                                                    PrepareFlags.None, "type", new byte[0], new byte[1024]));
            PrepareLogRecord read;
            cache.TryGetRecord(1, out read);
            Assert.AreEqual(1, cache.GetStatistics().HitCount);
        }

        [Test]
        public void statistics_are_updated_with_misses()
        {
            var cache = new DictionaryBasedCache(100, 1024 * 9);
            cache.PutRecord(1, new PrepareLogRecord(1, Guid.NewGuid(), _id, 1, 0, "test", 1, DateTime.UtcNow, 
                                                    PrepareFlags.None, "type", new byte[0], new byte[1024]));
            PrepareLogRecord read;
            cache.TryGetRecord(0, out read);
            Assert.AreEqual(1, cache.GetStatistics().MissCount);
        }

        [Test]
        public void statistics_are_updated_with_total_count()
        {
            var cache = new DictionaryBasedCache(100, 1024 * 9);
            cache.PutRecord(1, new PrepareLogRecord(1, Guid.NewGuid(), _id, 1, 0, "test", 1, DateTime.UtcNow, 
                                                    PrepareFlags.None, "type", new byte[0], new byte[1024]));
            PrepareLogRecord read;
            cache.TryGetRecord(0, out read);
            Assert.AreEqual(1, cache.GetStatistics().Count);
        }

        [Test]
        public void statistics_are_updated_with_total_size()
        {
            var cache = new DictionaryBasedCache(100, 1024 * 9);
            var record = new PrepareLogRecord(1, Guid.NewGuid(), _id, 1, 0, "test", 1, DateTime.UtcNow, 
                                              PrepareFlags.None, "type", new byte[0], new byte[1024]);
            cache.PutRecord(1, record);
            PrepareLogRecord read;
            cache.TryGetRecord(0, out read);
            Assert.AreEqual(record.InMemorySize, cache.GetStatistics().Size);
        }
    }
}