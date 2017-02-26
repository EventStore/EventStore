using System;
using System.Linq;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV1
{
    [TestFixture]
    public class ptable_should: SpecificationWithFilePerTestFixture
    {
        protected byte _ptableVersion = PTableVersions.IndexV1;
        private PTable _ptable;

        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            var table = new HashListMemTable(_ptableVersion, maxSize: 10);
            table.Add(0x010100000000, 0x0001, 0x0001);
            _ptable = PTable.FromMemtable(table, Filename, cacheDepth: 0);
        }

        public override void TestFixtureTearDown()
        {
            _ptable.Dispose();
            base.TestFixtureTearDown();
        }

        [Test]
        public void throw_argumentoutofrangeexception_on_range_query_when_provided_with_negative_start_version()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ptable.GetRange(0x0000, -1, long.MaxValue).ToArray());
        }

        [Test]
        public void throw_argumentoutofrangeexception_on_range_query_when_provided_with_negative_end_version()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ptable.GetRange(0x0000, 0, -1).ToArray());
        }

        [Test]
        public void throw_argumentoutofrangeexception_on_get_one_entry_query_when_provided_with_negative_version()
        {
            long pos;
            Assert.Throws<ArgumentOutOfRangeException>(() => _ptable.TryGetOneValue(0x0000, -1, out pos));
        }
    }
}