using System.Collections.Generic;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class when_merging_ptables_with_entries_to_nonexisting_record: SpecificationWithDirectoryPerTestFixture
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

                var table = new HashListMemTable(maxSize: 30);
                for (int j = 0; j < 10; j++)
                {
                    table.Add((uint)i, j, i*10 + j);
                }
                _tables.Add(PTable.FromMemtable(table, _files[i]));
            }
            _files.Add(GetTempFilePath());
            _newtable = PTable.MergeTo(_tables, _files[4], x => x.Position % 2 == 0);
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
        public void there_are_only_twenty_entries_left()
        {
            Assert.AreEqual(20, _newtable.Count);
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
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    long position;
                    if ((i*10 + j)%2 == 0)
                    {
                        Assert.IsTrue(_newtable.TryGetOneValue((uint)i, j, out position));
                        Assert.AreEqual(i*10+j, position);
                    }
                    else
                        Assert.IsFalse(_newtable.TryGetOneValue((uint)i, j, out position));
                }
            }
        }
    }
}