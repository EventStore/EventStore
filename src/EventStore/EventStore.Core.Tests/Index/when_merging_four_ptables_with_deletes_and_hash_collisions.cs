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
using System.IO;
using System.Linq;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class when_merging_four_ptables_with_deletes_and_hash_collisions
    {
        private readonly List<string> _files = new List<string>();
        private readonly List<PTable> _tables = new List<PTable>();
        private PTable _newtable;

        [TestFixtureSetUp]
        public void Setup()
        {
            for (int i = 0; i < 4; i++)
            {
                _files.Add(Path.GetRandomFileName());

                var table = new HashListMemTable();
                for (int j = 0; j < 10; j++)
                {
                    table.Add((UInt32)j%8, i, i * j * 100 + i + j); // 0 collisions with 8, 1 collisions with 9
                }
                if (i == 3)
                {
                    table.Add(0, int.MaxValue, 45);
                    table.Add(1, int.MaxValue, 45);
                    table.Add(2, int.MaxValue, 45);
                    table.Add(3, int.MaxValue, 45);
                }
                _tables.Add(PTable.FromMemtable(table, _files[i]));
            }
            _files.Add(Path.GetRandomFileName());
            _newtable = PTable.MergeTo(_tables, _files[4], x => x.Stream <= 1);
        }

        [Test]
        public void there_are_thirty_six_records_in_merged_index_because_collisions_are_not_deleted()
        {
            // 40 data records + 4 delete records - 8 deleted records (with 2 and 3 as a hash)
            Assert.AreEqual(36, _newtable.Count);
        }

        [Test]
        public void the_hash_can_be_verified()
        {
            Assert.DoesNotThrow(() => _newtable.VerifyFileHash());
        }

        [Test]
        public void the_items_are_sorted()
        {
            var last = new IndexEntry(ulong.MaxValue, long.MaxValue);
            foreach (var item in _newtable.IterateAllInOrder())
            {
                Assert.IsTrue(last.Key > item.Key || last.Key == item.Key && last.Position > item.Position);
                last = item;
            }
        }

        [Test]
        public void the_right_items_are_deleted()
        {
            long position;
            for (uint i = 2; i <= 3; i++)
            {
                Assert.IsFalse(_newtable.TryGetOneValue(i, 0, out position));
            }
        }

        [Test]
        public void the_items_with_hash_collision_are_preserved()
        {
            for (uint i = 0; i <= 1; i++)
            {
                var entries = _newtable.GetRange(i, 0, 0).ToArray();
                Assert.AreEqual(2, entries.Length);
            }
        }

        [TestFixtureTearDown]
        public void Teardown()
        {
            _newtable.Dispose();
            foreach (var ssTable in _tables)
            {
                ssTable.Dispose();
            }
            foreach (var f in _files)
            {
                File.Delete(f);
            }
        }
    }
}