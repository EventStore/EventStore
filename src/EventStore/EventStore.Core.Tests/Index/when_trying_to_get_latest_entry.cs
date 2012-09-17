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
using System.IO;
using System.Linq;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class when_trying_to_get_latest_entry: SpecificationWithFile
    {
        [Test]
        public void nothing_is_found_on_empty_stream()
        {
            var memTable = new HashListMemTable();
            memTable.Add(0x11, 0x01, 0xffff);
            using (var ptable = PTable.FromMemtable(memTable, Filename))
            {
                IndexEntry entry;
                Assert.IsFalse(ptable.TryGetLatestEntry(0x12, out entry));
            }
        }

        [Test]
        public void single_item_is_latest()
        {
            var memTable = new HashListMemTable();
            memTable.Add(0x11, 0x01, 0xffff);
            using (var ptable = PTable.FromMemtable(memTable, Filename))
            {
                IndexEntry entry;
                Assert.IsTrue(ptable.TryGetLatestEntry(0x11, out entry));
                Assert.AreEqual(0x11, entry.Stream);
                Assert.AreEqual(0x01, entry.Version);
                Assert.AreEqual(0xffff, entry.Position);
            }
        }

        [Test]
        public void correct_entry_is_returned()
        {
            var memTable = new HashListMemTable();
            memTable.Add(0x11, 0x01, 0xffff);
            memTable.Add(0x11, 0x02, 0xfff2);
            using (var ptable = PTable.FromMemtable(memTable, Filename))
            {
                IndexEntry entry;
                Assert.IsTrue(ptable.TryGetLatestEntry(0x11, out entry));
                Assert.AreEqual(0x11, entry.Stream);
                Assert.AreEqual(0x02, entry.Version);
                Assert.AreEqual(0xfff2, entry.Position);
            }
        }

        [Test]
        public void when_duplicated_entries_exist_the_one_with_latest_position_is_returned()
        {
            var memTable = new HashListMemTable();
            memTable.Add(0x11, 0x01, 0xfff1);
            memTable.Add(0x11, 0x02, 0xfff2);
            memTable.Add(0x11, 0x01, 0xfff3);
            memTable.Add(0x11, 0x02, 0xfff4);
            using (var ptable = PTable.FromMemtable(memTable, Filename))
            {
                IndexEntry entry;
                Assert.IsTrue(ptable.TryGetLatestEntry(0x11, out entry));
                Assert.AreEqual(0x11, entry.Stream);
                Assert.AreEqual(0x02, entry.Version);
                Assert.AreEqual(0xfff4, entry.Position);
            }
        }

        [Test]
        public void only_entry_with_largest_position_is_returned_when_triduplicated()
        {
            var memTable = new HashListMemTable();
            memTable.Add(0x11, 0x01, 0xfff1);
            memTable.Add(0x11, 0x01, 0xfff3);
            memTable.Add(0x11, 0x01, 0xfff5);
            using (var ptable = PTable.FromMemtable(memTable, Filename))
            {
                IndexEntry entry;
                Assert.IsTrue(ptable.TryGetLatestEntry(0x11, out entry));
                Assert.AreEqual(0x11, entry.Stream);
                Assert.AreEqual(0x01, entry.Version);
                Assert.AreEqual(0xfff5, entry.Position);
            }
        }
    }
}
