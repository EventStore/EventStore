using System;
using System.Linq;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class table_index_should : SpecificationWithDirectoryPerTestFixture
    {
        private TableIndex _tableIndex;

        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            _tableIndex = new TableIndex(PathName,
                                         () => new HashListMemTable(maxSize: 20),
                                         () => { throw new InvalidOperationException(); },
                                         maxSizeForMemory: 10);
            _tableIndex.Initialize(long.MaxValue);
        }

        public override void TestFixtureTearDown()
        {
            _tableIndex.Close(); 
            base.TestFixtureTearDown();
        }

        [Test]
        public void throw_argumentoutofrangeexception_on_range_query_when_provided_with_negative_start_version()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _tableIndex.GetRange(0x0000, -1, int.MaxValue).ToArray());
        }

        [Test]
        public void throw_argumentoutofrangeexception_on_range_query_when_provided_with_negative_end_version()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _tableIndex.GetRange(0x0000, 0, -1).ToArray());
        }

        [Test]
        public void throw_argumentoutofrangeexception_on_get_one_entry_query_when_provided_with_negative_version()
        {
            long pos;
            Assert.Throws<ArgumentOutOfRangeException>(() => _tableIndex.TryGetOneValue(0x0000, -1, out pos));
        }

        [Test]
        public void throw_argumentoutofrangeexception_on_adding_entry_with_negative_commit_position()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _tableIndex.Add(-1, 0x0000, 0, 0));
        }

        [Test]
        public void throw_argumentoutofrangeexception_on_adding_entry_with_negative_version()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _tableIndex.Add(0, 0x0000, -1, 0));
        }

        [Test]
        public void throw_argumentoutofrangeexception_on_adding_entry_with_negative_position()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _tableIndex.Add(0, 0x0000, 0, -1));
        }
    }
}