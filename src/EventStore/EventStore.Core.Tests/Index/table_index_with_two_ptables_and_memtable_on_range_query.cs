using System.Linq;
using System.Threading;
using EventStore.Core.Index;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.TransactionLog;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture, Category("LongRunning")]
    public class table_index_with_two_ptables_and_memtable_on_range_query : SpecificationWithDirectoryPerTestFixture
    {
        private TableIndex _tableIndex;
        private string _indexDir;

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            _indexDir = PathName;
            var fakeReader = new TFReaderLease(new FakeTfReader());
            _tableIndex = new TableIndex(_indexDir,
                                         () => new HashListMemTable(maxSize: 10),
                                         () => fakeReader,
                                         maxSizeForMemory: 2,
                                         maxTablesPerLevel: 2);
            _tableIndex.Initialize(long.MaxValue);

            // ptable level 2
            _tableIndex.Add(0, 0xDEAD, 0, 0xFF00); 
            _tableIndex.Add(0, 0xDEAD, 1, 0xFF01);
            _tableIndex.Add(0, 0xBEEF, 0, 0xFF00);
            _tableIndex.Add(0, 0xBEEF, 1, 0xFF01);
            _tableIndex.Add(0, 0xABBA, 0, 0xFF00);
            _tableIndex.Add(0, 0xABBA, 1, 0xFF01);
            _tableIndex.Add(0, 0xABBA, 0, 0xFF02);
            _tableIndex.Add(0, 0xABBA, 1, 0xFF03);

            // ptable level 1
            _tableIndex.Add(0, 0xADA, 0, 0xFF00);
            _tableIndex.Add(0, 0xCEED, 10, 0xFFF1);
            _tableIndex.Add(0, 0xBABA, 0, 0xFF00);
            _tableIndex.Add(0, 0xDEAD, 0, 0xFF10);

            // ptable level 0
            _tableIndex.Add(0, 0xBABA, 1, 0xFF01);
            _tableIndex.Add(0, 0xDEAD, 1, 0xFF11);

            // memtable
            _tableIndex.Add(0, 0xADA, 0, 0xFF01);

            Thread.Sleep(500);
        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            _tableIndex.Close();

            base.TestFixtureTearDown();
        }

        [Test]
        public void should_not_return_latest_entry_for_nonexisting_stream()
        {
            IndexEntry entry;
            Assert.IsFalse(_tableIndex.TryGetLatestEntry(0xFEED, out entry));
        }

        [Test]
        public void should_not_return_oldest_entry_for_nonexisting_stream()
        {
            IndexEntry entry;
            Assert.IsFalse(_tableIndex.TryGetLatestEntry(0xFEED, out entry));
        }

        [Test]
        public void should_return_correct_latest_entry_for_stream_with_latest_entry_in_memtable()
        {
            IndexEntry entry;
            Assert.IsTrue(_tableIndex.TryGetLatestEntry(0xADA, out entry));
            Assert.AreEqual(0xADA, entry.Stream);
            Assert.AreEqual(0, entry.Version);
            Assert.AreEqual(0xFF01, entry.Position);
        }

        [Test]
        public void should_return_correct_latest_entry_for_stream_with_latest_entry_in_ptable_0()
        {
            IndexEntry entry;
            Assert.IsTrue(_tableIndex.TryGetLatestEntry(0xDEAD, out entry));
            Assert.AreEqual(0xDEAD, entry.Stream);
            Assert.AreEqual(1, entry.Version);
            Assert.AreEqual(0xFF11, entry.Position);
        }

        [Test]
        public void should_return_correct_latest_entry_for_another_stream_with_latest_entry_in_ptable_0()
        {
            IndexEntry entry;
            Assert.IsTrue(_tableIndex.TryGetLatestEntry(0xBABA, out entry));
            Assert.AreEqual(0xBABA, entry.Stream);
            Assert.AreEqual(1, entry.Version);
            Assert.AreEqual(0xFF01, entry.Position);
        }

        [Test]
        public void should_return_correct_latest_entry_for_stream_with_latest_entry_in_ptable_1()
        {
            IndexEntry entry;
            Assert.IsTrue(_tableIndex.TryGetLatestEntry(0xCEED, out entry));
            Assert.AreEqual(0xCEED, entry.Stream);
            Assert.AreEqual(10, entry.Version);
            Assert.AreEqual(0xFFF1, entry.Position);
        }

        [Test]
        public void should_return_correct_latest_entry_for_stream_with_latest_entry_in_ptable_2()
        {
            IndexEntry entry;
            Assert.IsTrue(_tableIndex.TryGetLatestEntry(0xBEEF, out entry));
            Assert.AreEqual(0xBEEF, entry.Stream);
            Assert.AreEqual(1, entry.Version);
            Assert.AreEqual(0xFF01, entry.Position);
        }

        [Test]
        public void should_return_correct_latest_entry_for_another_stream_with_latest_entry_in_ptable_2()
        {
            IndexEntry entry;
            Assert.IsTrue(_tableIndex.TryGetLatestEntry(0xABBA, out entry));
            Assert.AreEqual(0xABBA, entry.Stream);
            Assert.AreEqual(1, entry.Version);
            Assert.AreEqual(0xFF03, entry.Position);
        }

        [Test]
        public void should_return_correct_oldest_entries_for_each_stream()
        {
            IndexEntry entry;

            Assert.IsTrue(_tableIndex.TryGetOldestEntry(0xDEAD, out entry));
            Assert.AreEqual(0xDEAD, entry.Stream);
            Assert.AreEqual(0, entry.Version);
            Assert.AreEqual(0xFF00, entry.Position);

            Assert.IsTrue(_tableIndex.TryGetOldestEntry(0xBEEF, out entry));
            Assert.AreEqual(0xBEEF, entry.Stream);
            Assert.AreEqual(0, entry.Version);
            Assert.AreEqual(0xFF00, entry.Position);

            Assert.IsTrue(_tableIndex.TryGetOldestEntry(0xABBA, out entry));
            Assert.AreEqual(0xABBA, entry.Stream);
            Assert.AreEqual(0, entry.Version);
            Assert.AreEqual(0xFF00, entry.Position);

            Assert.IsTrue(_tableIndex.TryGetOldestEntry(0xADA, out entry));
            Assert.AreEqual(0xADA, entry.Stream);
            Assert.AreEqual(0, entry.Version);
            Assert.AreEqual(0xFF00, entry.Position);

            Assert.IsTrue(_tableIndex.TryGetOldestEntry(0xCEED, out entry));
            Assert.AreEqual(0xCEED, entry.Stream);
            Assert.AreEqual(10, entry.Version);
            Assert.AreEqual(0xFFF1, entry.Position);

            Assert.IsTrue(_tableIndex.TryGetOldestEntry(0xBABA, out entry));
            Assert.AreEqual(0xBABA, entry.Stream);
            Assert.AreEqual(0, entry.Version);
            Assert.AreEqual(0xFF00, entry.Position);
        }

        [Test]
        public void should_return_empty_range_for_nonexisting_stream()
        {
            var range = _tableIndex.GetRange(0xFEED, 0, int.MaxValue).ToArray();
            Assert.AreEqual(0, range.Length);
        }

        [Test]
        public void should_return_correct_full_range_with_descending_order_for_0xDEAD()
        {
            var range = _tableIndex.GetRange(0xDEAD, 0, int.MaxValue).ToArray();
            Assert.AreEqual(4, range.Length);
            Assert.AreEqual(new IndexEntry(0xDEAD, 1, 0xFF11), range[0]);
            Assert.AreEqual(new IndexEntry(0xDEAD, 1, 0xFF01), range[1]);
            Assert.AreEqual(new IndexEntry(0xDEAD, 0, 0xFF10), range[2]);
            Assert.AreEqual(new IndexEntry(0xDEAD, 0, 0xFF00), range[3]);
        }

        [Test]
        public void should_return_correct_full_range_with_descending_order_for_0xBEEF()
        {
            var range = _tableIndex.GetRange(0xBEEF, 0, int.MaxValue).ToArray();
            Assert.AreEqual(2, range.Length);
            Assert.AreEqual(new IndexEntry(0xBEEF, 1, 0xFF01), range[0]);
            Assert.AreEqual(new IndexEntry(0xBEEF, 0, 0xFF00), range[1]);
        }

        [Test]
        public void should_return_correct_full_range_with_descending_order_for_0xABBA()
        {
            var range = _tableIndex.GetRange(0xABBA, 0, int.MaxValue).ToArray();
            Assert.AreEqual(4, range.Length);
            Assert.AreEqual(new IndexEntry(0xABBA, 1, 0xFF03), range[0]);
            Assert.AreEqual(new IndexEntry(0xABBA, 1, 0xFF01), range[1]);
            Assert.AreEqual(new IndexEntry(0xABBA, 0, 0xFF02), range[2]);
            Assert.AreEqual(new IndexEntry(0xABBA, 0, 0xFF00), range[3]);
        }

        [Test]
        public void should_return_correct_full_range_with_descending_order_for_0xADA()
        {
            var range = _tableIndex.GetRange(0xADA, 0, int.MaxValue).ToArray();
            Assert.AreEqual(2, range.Length);
            Assert.AreEqual(new IndexEntry(0xADA, 0, 0xFF01), range[0]);
            Assert.AreEqual(new IndexEntry(0xADA, 0, 0xFF00), range[1]);
        }

        [Test]
        public void should_return_correct_full_range_with_descending_order_for_0xCEED()
        {
            var range = _tableIndex.GetRange(0xCEED, 0, int.MaxValue).ToArray();
            Assert.AreEqual(1, range.Length);
            Assert.AreEqual(new IndexEntry(0xCEED, 10, 0xFFF1), range[0]);
        }

        [Test]
        public void should_return_correct_full_range_with_descending_order_for_0xBABA()
        {
            var range = _tableIndex.GetRange(0xBABA, 0, int.MaxValue).ToArray();
            Assert.AreEqual(2, range.Length);
            Assert.AreEqual(new IndexEntry(0xBABA, 1, 0xFF01), range[0]);
            Assert.AreEqual(new IndexEntry(0xBABA, 0, 0xFF00), range[1]);
        }
     
        [Test]
        public void should_not_return_one_value_for_nonexistent_stream()
        {
            long pos;
            Assert.IsFalse(_tableIndex.TryGetOneValue(0xFEED, 0, out pos));
        }

        [Test]
        public void should_return_one_value_for_existing_streams_for_existing_version()
        {
            long pos;
            Assert.IsTrue(_tableIndex.TryGetOneValue(0xDEAD, 1, out pos));
            Assert.AreEqual(0xFF11, pos);

            Assert.IsTrue(_tableIndex.TryGetOneValue(0xBEEF, 0, out pos));
            Assert.AreEqual(0xFF00, pos);

            Assert.IsTrue(_tableIndex.TryGetOneValue(0xABBA, 0, out pos));
            Assert.AreEqual(0xFF02, pos);

            Assert.IsTrue(_tableIndex.TryGetOneValue(0xADA, 0, out pos));
            Assert.AreEqual(0xFF01, pos);
            
            Assert.IsTrue(_tableIndex.TryGetOneValue(0xCEED, 10, out pos));
            Assert.AreEqual(0xFFF1, pos);
            
            Assert.IsTrue(_tableIndex.TryGetOneValue(0xBABA, 1, out pos));
            Assert.AreEqual(0xFF01, pos);
        }

        [Test]
        public void should_not_return_one_value_for_existing_streams_for_nonexistent_version()
        {
            long pos;
            Assert.IsFalse(_tableIndex.TryGetOneValue(0xDEAD, 2, out pos));
            Assert.IsFalse(_tableIndex.TryGetOneValue(0xBEEF, 2, out pos));
            Assert.IsFalse(_tableIndex.TryGetOneValue(0xABBA, 2, out pos));
            Assert.IsFalse(_tableIndex.TryGetOneValue(0xADA, 1, out pos));
            Assert.IsFalse(_tableIndex.TryGetOneValue(0xCEED, 0, out pos));
            Assert.IsFalse(_tableIndex.TryGetOneValue(0xBABA, 2, out pos));
        }

    }
}