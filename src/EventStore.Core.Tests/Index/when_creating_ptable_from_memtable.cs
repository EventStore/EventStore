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