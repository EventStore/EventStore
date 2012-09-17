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
using System.Linq;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class searching_ptable_with_items_spanning_few_cache_segments_and_all_items_in_cache : ptable_read_scenario_with_items_spanning_few_cache_segments
    {
        public searching_ptable_with_items_spanning_few_cache_segments_and_all_items_in_cache()
            : base(midpointCacheDepth: 10)
        {

        }
    }

    [TestFixture]
    public class searching_ptable_with_items_spanning_few_cache_segments_and_only_some_items_in_cache : ptable_read_scenario_with_items_spanning_few_cache_segments
    {
        public searching_ptable_with_items_spanning_few_cache_segments_and_only_some_items_in_cache()
            : base(midpointCacheDepth: 0)
        {
        }
    }

    [TestFixture]
    public abstract class ptable_read_scenario_with_items_spanning_few_cache_segments : PTableReadScenario
    {
        protected ptable_read_scenario_with_items_spanning_few_cache_segments(int midpointCacheDepth)
            : base(midpointCacheDepth)
        {
        }

        protected override void AddItemsForScenario(IMemTable memTable)
        {
            memTable.Add(0, 0, 0x0001);
            memTable.Add(0, 0, 0x0002);
            memTable.Add(1, 0, 0x0003);
            memTable.Add(1, 0, 0x0004);
            memTable.Add(1, 0, 0x0005);
        }

        [Test]
        public void the_table_has_five_items()
        {
            Assert.AreEqual(5, PTable.Count);
        }

        [Test]
        public void the_smallest_items_can_be_found()
        {
            long position;
            Assert.IsTrue(PTable.TryGetOneValue(0, 0, out position));
            Assert.AreEqual(0x0002, position);
        }

        [Test]
        public void the_smallest_items_are_returned_in_descending_order()
        {
            var entries = PTable.GetRange(0, 0, 0).ToArray();
            Assert.AreEqual(2, entries.Length);
            Assert.AreEqual(0, entries[0].Stream);
            Assert.AreEqual(0, entries[0].Version);
            Assert.AreEqual(0x0002, entries[0].Position);
            Assert.AreEqual(0, entries[1].Stream);
            Assert.AreEqual(0, entries[1].Version);
            Assert.AreEqual(0x0001, entries[1].Position);
        }

        [Test]
        public void try_get_latest_entry_for_smallest_hash_returns_correct_index_entry()
        {
            IndexEntry entry;
            Assert.IsTrue(PTable.TryGetLatestEntry(0, out entry));
            Assert.AreEqual(0, entry.Stream);
            Assert.AreEqual(0, entry.Version);
            Assert.AreEqual(0x0002, entry.Position);
        }

        [Test]
        public void the_largest_items_can_be_found()
        {
            long position;
            Assert.IsTrue(PTable.TryGetOneValue(1, 0, out position));
            Assert.AreEqual(0x0005, position);
        }

        [Test]
        public void the_largest_items_are_returned_in_descending_order()
        {
            var entries = PTable.GetRange(1, 0, 0).ToArray();
            Assert.AreEqual(3, entries.Length);
            Assert.AreEqual(1, entries[0].Stream);
            Assert.AreEqual(0, entries[0].Version);
            Assert.AreEqual(0x0005, entries[0].Position);
            Assert.AreEqual(1, entries[1].Stream);
            Assert.AreEqual(0, entries[1].Version);
            Assert.AreEqual(0x0004, entries[1].Position);
            Assert.AreEqual(1, entries[2].Stream);
            Assert.AreEqual(0, entries[2].Version);
            Assert.AreEqual(0x0003, entries[2].Position);
        }

        [Test]
        public void try_get_latest_entry_for_largest_hash_returns_correct_index_entry()
        {
            IndexEntry entry;
            Assert.IsTrue(PTable.TryGetLatestEntry(1, out entry));
            Assert.AreEqual(1, entry.Stream);
            Assert.AreEqual(0, entry.Version);
            Assert.AreEqual(0x0005, entry.Position);
        }

        [Test]
        public void non_existent_item_cannot_be_found()
        {
            long position;
            Assert.IsFalse(PTable.TryGetOneValue(2, 0, out position));
        }

        [Test]
        public void range_query_returns_nothing_for_nonexistent_stream()
        {
            var entries = PTable.GetRange(2, 0, int.MaxValue).ToArray();
            Assert.AreEqual(0, entries.Length);
        }

        [Test]
        public void try_get_latest_entry_returns_nothing_for_nonexistent_stream()
        {
            IndexEntry entry;
            Assert.IsFalse(PTable.TryGetLatestEntry(2, out entry));
        }
    }
}
