using System;
using System.Collections.Generic;
using EventStore.Core.Index;
using NUnit.Framework;
using EventStore.Core.Index.Hashes;

namespace EventStore.Core.Tests.Index.Index32Bit
{
    [TestFixture]
    public class when_merging_ptables : SpecificationWithDirectoryPerTestFixture
    {
        private readonly List<string> _files = new List<string>();
        private readonly List<PTable> _tables = new List<PTable>();

        private PTable _newtable;

        [OneTimeSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            _files.Add(GetTempFilePath());
            var table = new HashListMemTable(PTableVersions.Index32Bit, maxSize: 20);
            table.Add(0x010100000000, 0, 0x0101);
            table.Add(0x010200000000, 0, 0x0102);
            table.Add(0x010300000000, 0, 0x0103);
            table.Add(0x010400000000, 0, 0x0104);
            _tables.Add(PTable.FromMemtable(table, GetTempFilePath()));
            table = new HashListMemTable(PTableVersions.Index32Bit, maxSize: 20);
            table.Add(0x010500000000, 0, 0x0105);
            table.Add(0x010600000000, 0, 0x0106);
            table.Add(0x010700000000, 0, 0x0107);
            table.Add(0x010800000000, 0, 0x0108);
            _tables.Add(PTable.FromMemtable(table, GetTempFilePath()));
            _newtable = PTable.MergeTo(_tables, GetTempFilePath(), (streamId, hash) => hash + 1, x => true, x => new Tuple<string, bool>(x.Stream.ToString(), true), PTableVersions.Index32Bit);
        }

        [OneTimeTearDown]
        public override void TestFixtureTearDown()
        {
            _newtable.Dispose();
            foreach (var ssTable in _tables)
            {
                ssTable.Dispose();
            }
            base.TestFixtureTearDown();
        }

        [Test]
        public void merged_ptable_is_32bit()
        {
            Assert.AreEqual(PTableVersions.Index32Bit, _newtable.Version);
        }

        [Test]
        public void there_are_8_records_in_the_merged_index()
        {
            Assert.AreEqual(8, _newtable.Count);
        }

        [Test]
        public void no_entries_should_have_upgraded_hashes()
        {
            foreach (var item in _newtable.IterateAllInOrder())
            {
                Assert.IsTrue((ulong)item.Position == item.Stream, "Expected the Stream (Hash) {0} to be equal to {1}", item.Stream, item.Position);
            }
        }
    }

    [TestFixture]
    public class when_merging_ptables_to_64bit: SpecificationWithDirectoryPerTestFixture
    {
        private readonly List<string> _files = new List<string>();
        private readonly List<PTable> _tables = new List<PTable>();

        private PTable _newtable;

        [OneTimeSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            _files.Add(GetTempFilePath());
            var table = new HashListMemTable(PTableVersions.Index32Bit, maxSize: 20);
            table.Add(0x010100000000, 0, 0x0101);
            table.Add(0x010200000000, 0, 0x0102);
            table.Add(0x010300000000, 0, 0x0103);
            table.Add(0x010400000000, 0, 0x0104);
            _tables.Add(PTable.FromMemtable(table, GetTempFilePath()));
            table = new HashListMemTable(PTableVersions.Index32Bit, maxSize: 20);
            table.Add(0x010500000000, 0, 0x0105);
            table.Add(0x010600000000, 0, 0x0106);
            table.Add(0x010700000000, 0, 0x0107);
            table.Add(0x010800000000, 0, 0x0108);
            _tables.Add(PTable.FromMemtable(table, GetTempFilePath()));
            _newtable = PTable.MergeTo(_tables, GetTempFilePath(), (streamId, hash) => hash + 1, x => true, x => new Tuple<string, bool>(x.Stream.ToString(), true), PTableVersions.Index64Bit);
        }

        [OneTimeTearDown]
        public override void TestFixtureTearDown()
        {
            _newtable.Dispose();
            foreach (var ssTable in _tables)
            {
                ssTable.Dispose();
            }
            base.TestFixtureTearDown();
        }

        [Test]
        public void merged_ptable_is_64bit()
        {
            Assert.AreEqual(PTableVersions.Index64Bit, _newtable.Version);
        }

        [Test]
        public void there_are_8_records_in_the_merged_index()
        {
            Assert.AreEqual(8, _newtable.Count);
        }

        [Test]
        public void all_the_entries_have_upgraded_hashes()
        {
            foreach (var item in _newtable.IterateAllInOrder())
            {
                Assert.IsTrue((ulong)item.Position == item.Stream - 1, "Expected the Stream (Hash) {0} to be equal to {1}", item.Stream - 1, item.Position);
            }
        }
    }

    [TestFixture]
    public class when_merging_2_32bit_ptables_and_1_64bit_ptable_to_64bit : SpecificationWithDirectoryPerTestFixture
    {
        private readonly List<string> _files = new List<string>();
        private readonly List<PTable> _tables = new List<PTable>();

        private PTable _newtable;

        [OneTimeSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            _files.Add(GetTempFilePath());
            var table = new HashListMemTable(PTableVersions.Index32Bit, maxSize: 20);
            table.Add(0x010100000000, 0, 0x010100000000);
            table.Add(0x010200000000, 0, 0x010200000000);
            table.Add(0x010300000000, 0, 0x010300000000);
            table.Add(0x010400000000, 0, 0x010400000000);
            _tables.Add(PTable.FromMemtable(table, GetTempFilePath()));
            table = new HashListMemTable(PTableVersions.Index32Bit, maxSize: 20);
            table.Add(0x010500000000, 0, 0x010500000000);
            table.Add(0x010600000000, 0, 0x010600000000);
            table.Add(0x010700000000, 0, 0x010700000000);
            table.Add(0x010800000000, 0, 0x010800000000);
            _tables.Add(PTable.FromMemtable(table, GetTempFilePath()));
            table = new HashListMemTable(PTableVersions.Index64Bit, maxSize: 20);
            table.Add(0x010900000000, 0, 0x010900000000);
            table.Add(0x101000000000, 0, 0x101000000000);
            table.Add(0x111000000000, 0, 0x111000000000);
            table.Add(0x121000000000, 0, 0x121000000000);
            _tables.Add(PTable.FromMemtable(table, GetTempFilePath()));
            _newtable = PTable.MergeTo(_tables, GetTempFilePath(), (streamId, hash) => hash + 1, x => true, x => new Tuple<string, bool>(x.Stream.ToString(), true), PTableVersions.Index64Bit);
        }

        [OneTimeTearDown]
        public override void TestFixtureTearDown()
        {
            _newtable.Dispose();
            foreach (var ssTable in _tables)
            {
                ssTable.Dispose();
            }
            base.TestFixtureTearDown();
        }

        [Test]
        public void merged_ptable_is_64bit()
        {
            Assert.AreEqual(PTableVersions.Index64Bit, _newtable.Version);
        }

        [Test]
        public void there_are_12_records_in_the_merged_index()
        {
            Assert.AreEqual(12, _newtable.Count);
        }

        [Test]
        public void only_the_32_bit_index_entries_should_have_upgraded_hashes()
        {
            foreach (var item in _newtable.IterateAllInOrder())
            {
                if(item.Position >= 0x010900000000) //these are 64bit already
                {
                    Assert.IsTrue((ulong)item.Position == item.Stream, "Expected the Stream (Hash) {0} to be equal to {1}", item.Stream, item.Position);
                }
                else
                {
                    Assert.IsTrue((ulong)item.Position >> 32 == item.Stream - 1, "Expected the Stream (Hash) {0} to be equal to {1}", item.Stream - 1, item.Position);
                }
            }
        }
    }

    [TestFixture]
    public class when_merging_1_32bit_ptables_and_1_64bit_ptable_with_missing_entries_to_64bit : SpecificationWithDirectoryPerTestFixture
    {
        private readonly List<string> _files = new List<string>();
        private readonly List<PTable> _tables = new List<PTable>();
        private IHasher hasher;

        private PTable _newtable;

        [OneTimeSetUp]
        public override void TestFixtureSetUp()
        {
            hasher = new Murmur3AUnsafe();
            base.TestFixtureSetUp();
            _files.Add(GetTempFilePath());
            var table = new HashListMemTable(PTableVersions.Index64Bit, maxSize: 20);
            table.Add(0x010100000000, 2, 5);
            table.Add(0x010200000000, 1, 6);
            table.Add(0x010200000000, 2, 7);
            table.Add(0x010400000000, 0, 8);
            table.Add(0x010400000000, 1, 9);
            _tables.Add(PTable.FromMemtable(table, GetTempFilePath()));
            table = new HashListMemTable(PTableVersions.Index32Bit, maxSize: 20);
            table.Add(0x010100000000, 1, 10);
            table.Add(0x010100000000, 2, 11);
            table.Add(0x010500000000, 1, 12);
            table.Add(0x010500000000, 2, 13);
            table.Add(0x010500000000, 3, 14);
            _tables.Add(PTable.FromMemtable(table, GetTempFilePath()));
            _newtable = PTable.MergeTo(_tables, GetTempFilePath(), (streamId, hash) => hash << 32 | hasher.Hash(streamId), x => x.Position % 2 == 0, x => new Tuple<string, bool>(x.Stream.ToString(), x.Position % 2 == 0), PTableVersions.Index64Bit);
        }

        [OneTimeTearDown]
        public override void TestFixtureTearDown()
        {
            _newtable.Dispose();
            foreach (var ssTable in _tables)
            {
                ssTable.Dispose();
            }
            base.TestFixtureTearDown();
        }

        [Test]
        public void merged_ptable_is_64bit()
        {
            Assert.AreEqual(PTableVersions.Index64Bit, _newtable.Version);
        }

        [Test]
        public void there_are_5_records_in_the_merged_index()
        {
            Assert.AreEqual(5, _newtable.Count);
        }

        [Test]
        public void the_items_are_sorted()
        {
            var last = new IndexEntry(ulong.MaxValue, 0, long.MaxValue);
            foreach (var item in _newtable.IterateAllInOrder())
            {
                Assert.IsTrue((last.Stream == item.Stream ? last.Version > item.Version : last.Stream > item.Stream) || 
                ((last.Stream == item.Stream && last.Version == item.Version) && last.Position > item.Position));
                last = item;
            }
        }
    }

    [TestFixture]
    public class when_merging_2_32bit_ptables_and_1_64bit_ptable_with_missing_entries_to_64bit : SpecificationWithDirectoryPerTestFixture
    {
        private readonly List<string> _files = new List<string>();
        private readonly List<PTable> _tables = new List<PTable>();
        private IHasher hasher;

        private PTable _newtable;

        [OneTimeSetUp]
        public override void TestFixtureSetUp()
        {
            hasher = new Murmur3AUnsafe();
            base.TestFixtureSetUp();
            _files.Add(GetTempFilePath());
            var table = new HashListMemTable(PTableVersions.Index32Bit, maxSize: 20);
            table.Add(0x010100000000, 0, 1);
            table.Add(0x010200000000, 0, 2);
            table.Add(0x010300000000, 0, 3);
            table.Add(0x010300000000, 1, 4);
            _tables.Add(PTable.FromMemtable(table, GetTempFilePath()));
            table = new HashListMemTable(PTableVersions.Index64Bit, maxSize: 20);
            table.Add(0x010100000000, 2, 5);
            table.Add(0x010200000000, 1, 6);
            table.Add(0x010200000000, 2, 7);
            table.Add(0x010400000000, 0, 8);
            table.Add(0x010400000000, 1, 9);
            _tables.Add(PTable.FromMemtable(table, GetTempFilePath()));
            table = new HashListMemTable(PTableVersions.Index32Bit, maxSize: 20);
            table.Add(0x010100000000, 1, 10);
            table.Add(0x010100000000, 2, 11);
            table.Add(0x010500000000, 1, 12);
            table.Add(0x010500000000, 2, 13);
            table.Add(0x010500000000, 3, 14);
            _tables.Add(PTable.FromMemtable(table, GetTempFilePath()));
            _newtable = PTable.MergeTo(_tables, GetTempFilePath(), (streamId, hash) => hash << 32 | hasher.Hash(streamId), x => x.Position % 2 == 0, x => new Tuple<string, bool>(x.Stream.ToString(), x.Position % 2 == 0), PTableVersions.Index64Bit);
        }

        [OneTimeTearDown]
        public override void TestFixtureTearDown()
        {
            _newtable.Dispose();
            foreach (var ssTable in _tables)
            {
                ssTable.Dispose();
            }
            base.TestFixtureTearDown();
        }

        [Test]
        public void merged_ptable_is_64bit()
        {
            Assert.AreEqual(PTableVersions.Index64Bit, _newtable.Version);
        }

        [Test]
        public void there_are_7_records_in_the_merged_index()
        {
            Assert.AreEqual(7, _newtable.Count);
        }

        [Test]
        public void the_items_are_sorted()
        {
            var last = new IndexEntry(ulong.MaxValue, 0, long.MaxValue);
            foreach (var item in _newtable.IterateAllInOrder())
            {
                Assert.IsTrue((last.Stream == item.Stream ? last.Version > item.Version : last.Stream > item.Stream) || 
                    ((last.Stream == item.Stream && last.Version == item.Version) && last.Position > item.Position));
                last = item;
            }
        }
    }
}