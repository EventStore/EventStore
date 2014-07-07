using System;
using System.Collections.Generic;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class when_merging_four_ptables: SpecificationWithDirectoryPerTestFixture
    {
        private readonly List<string> _files = new List<string>();
        private readonly List<PTable> _tables = new List<PTable>();
        private PTable _newtable;

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            for (int i = 0; i < 4; i++)
            {
                _files.Add(GetTempFilePath());

                var table = new HashListMemTable(maxSize: 20);
                for (int j=0;j<10;j++)
                {
                    table.Add((UInt32) j + 1, i + 1, i*j);
                }
                _tables.Add(PTable.FromMemtable(table, _files[i]));
            }
            _files.Add(GetTempFilePath());
            _newtable = PTable.MergeTo(_tables, _files[4], _ => true);
        }

        [TestFixtureTearDown]
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
        public void there_are_forty_records_in_merged_index()
        {
            Assert.AreEqual(40, _newtable.Count);
        }

        [Test]
        public void the_items_are_sorted()
        {
            var last = new IndexEntry(ulong.MaxValue, long.MaxValue);
            foreach(var item in _newtable.IterateAllInOrder())
            {
                Assert.IsTrue(last.Key > item.Key || last.Key == item.Key && last.Position > item.Position);
                last = item;
            }
        }

        [Test]
        public void the_hash_can_be_verified()
        {
            Assert.DoesNotThrow(() => _newtable.VerifyFileHash());
        }
    }
}