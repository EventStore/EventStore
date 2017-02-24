using System.Collections.Generic;
using EventStore.Core.Index;
using NUnit.Framework;
using System;
using System.Linq;

namespace EventStore.Core.Tests.Index.IndexV1
{
    [TestFixture]
    public class when_merging_ptables_with_entries_to_nonexisting_record: SpecificationWithDirectoryPerTestFixture
    {
        private readonly List<string> _files = new List<string>();
        private readonly List<PTable> _tables = new List<PTable>();
        private PTable _newtable;
        protected byte _ptableVersion = PTableVersions.IndexV1;

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            for (int i = 0; i < 4; i++)
            {
                _files.Add(GetTempFilePath());

                var table = new HashListMemTable(_ptableVersion, maxSize: 30);
                for (int j = 0; j < 10; j++)
                {
                    table.Add((ulong)(0x010100000000 << i), j, i*10 + j);
                }
                _tables.Add(PTable.FromMemtable(table, _files[i]));
            }
            _files.Add(GetTempFilePath());
            _newtable = PTable.MergeTo(_tables, _files[4], (streamId, hash) => hash, x => x.Position % 2 == 0, x => new Tuple<string, bool>("", x.Position % 2 == 0), _ptableVersion);
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
                        Assert.IsTrue(_newtable.TryGetOneValue((ulong)(0x010100000000 << i), j, out position));
                        Assert.AreEqual(i*10+j, position);
                    }
                    else
                        Assert.IsFalse(_newtable.TryGetOneValue((ulong)(0x010100000000 << i), j, out position));
                }
            }
        }
    }
}