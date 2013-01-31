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
    public class when_creating_ptable_from_memtable: SpecificationWithFile
    {
        [Test]
        public void null_file_throws_null_exception()
        {
            Assert.Throws<ArgumentNullException>(() => PTable.FromMemtable(new HashListMemTable(maxSize: 10), null));
        }

        [Test]
        public void null_memtable_throws_null_exception()
        {
            Assert.Throws<ArgumentNullException>(() => PTable.FromMemtable(null, "C:\\foo.txt"));
        }

        [Test]
        public void wait_for_destroy_will_timeout()
        {
            var table = new HashListMemTable(maxSize: 10);
            table.Add(0x0101, 0x0001, 0x0001);
            var ptable = PTable.FromMemtable(table, Filename);
            Assert.Throws<TimeoutException>(() => ptable.WaitForDisposal(1));

            // tear down
            ptable.MarkForDestruction();
            ptable.WaitForDisposal(1000);
        }

        //[Test]
        //public void non_power_of_two_throws_invalid_operation()
        //{
        //    var table = new HashListMemTable();
        //    table.Add(0x0101, 0x0001, 0x0001);
        //    table.Add(0x0105, 0x0001, 0x0002);
        //    table.Add(0x0102, 0x0001, 0x0003);
        //    Assert.Throws<InvalidOperationException>(() => PTable.FromMemtable(table, "C:\\foo.txt"));
        //}

        [Test]
        public void the_file_gets_created()
        {
            var table = new HashListMemTable(maxSize: 10);
            table.Add(0x0101, 0x0001, 0x0001);
            table.Add(0x0105, 0x0001, 0x0002);
            table.Add(0x0102, 0x0001, 0x0003);
            table.Add(0x0102, 0x0002, 0x0003);
            using (var sstable = PTable.FromMemtable(table, Filename))
            {
                var fileinfo = new FileInfo(Filename);
                Assert.AreEqual(PTableHeader.Size + PTable.MD5Size + 4*16, fileinfo.Length);
                var items = sstable.IterateAllInOrder().ToList();
                Assert.AreEqual(0x0105, items[0].Stream);
                Assert.AreEqual(0x0001, items[0].Version);
                Assert.AreEqual(0x0102, items[1].Stream);
                Assert.AreEqual(0x0002, items[1].Version);
                Assert.AreEqual(0x0102, items[2].Stream);
                Assert.AreEqual(0x0001, items[2].Version);
                Assert.AreEqual(0x0101, items[3].Stream);
                Assert.AreEqual(0x0001, items[3].Version);        
            }
        }

        [Test]
        public void the_hash_of_file_is_valid()
        {
            var table = new HashListMemTable(maxSize: 10);
            table.Add(0x0101, 0x0001, 0x0001);
            table.Add(0x0105, 0x0001, 0x0002);
            table.Add(0x0102, 0x0001, 0x0003);
            table.Add(0x0102, 0x0002, 0x0003);
            using (var sstable = PTable.FromMemtable(table, Filename))
            {
                Assert.DoesNotThrow(() => sstable.VerifyFileHash());
            }
        }
    }
}