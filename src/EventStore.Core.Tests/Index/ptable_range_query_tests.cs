using System.Linq;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class ptable_range_query_tests: SpecificationWithFilePerTestFixture
    {
        private PTable _ptable;

        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            var table = new HashListMemTable(maxSize: 50);
            table.Add(0x0101, 0x0001, 0x0001);
            table.Add(0x0105, 0x0001, 0x0002);
            table.Add(0x0102, 0x0001, 0x0003);
            table.Add(0x0102, 0x0002, 0x0004);
            table.Add(0x0103, 0x0001, 0xFFF1);
            table.Add(0x0103, 0x0003, 0xFFF3);
            table.Add(0x0103, 0x0005, 0xFFF5);
            _ptable = PTable.FromMemtable(table, Filename, cacheDepth: 0);
        }

        public override void TestFixtureTearDown()
        {
            _ptable.Dispose();
            base.TestFixtureTearDown();
        }

        [Test]
        public void range_query_of_non_existing_stream_returns_nothing()
        {
            var list = _ptable.GetRange(0x14, 0x01, 0x02).ToArray();
            Assert.AreEqual(0, list.Length);
        }

        [Test]
        public void range_query_of_non_existing_version_returns_nothing()
        {
            var list = _ptable.GetRange(0x0101, 0x03, 0x05).ToArray();
            Assert.AreEqual(0, list.Length);
        }

        [Test]
        public void range_query_with_hole_returns_items_included()
        {
            var list = _ptable.GetRange(0x0103, 0x01, 0x05).ToArray();
            Assert.AreEqual(3, list.Length);
            Assert.AreEqual(0x0103, list[0].Stream);
            Assert.AreEqual(0x05, list[0].Version);
            Assert.AreEqual(0xfff5, list[0].Position);
            Assert.AreEqual(0x0103, list[1].Stream);
            Assert.AreEqual(0x03, list[1].Version);
            Assert.AreEqual(0xfff3, list[1].Position);
            Assert.AreEqual(0x0103, list[2].Stream);
            Assert.AreEqual(0x01, list[2].Version);
            Assert.AreEqual(0xfff1, list[2].Position);
        }

        [Test]
        public void query_with_start_in_range_but_not_end_results_returns_items_included()
        {
            var list = _ptable.GetRange(0x0103, 0x01, 0x04).ToArray();
            Assert.AreEqual(2, list.Length);
            Assert.AreEqual(0x0103, list[0].Stream);
            Assert.AreEqual(0x03, list[0].Version);
            Assert.AreEqual(0xfff3, list[0].Position);
            Assert.AreEqual(0x0103, list[1].Stream);
            Assert.AreEqual(0x01, list[1].Version);
            Assert.AreEqual(0xfff1, list[1].Position);
        }

        [Test]
        public void query_with_end_in_range_but_not_start_results_returns_items_included()
        {
            var list = _ptable.GetRange(0x0103, 0x00, 0x03).ToArray();
            Assert.AreEqual(2, list.Length);
            Assert.AreEqual(0x0103, list[0].Stream);
            Assert.AreEqual(0x03, list[0].Version);
            Assert.AreEqual(0xfff3, list[0].Position);
            Assert.AreEqual(0x0103, list[1].Stream);
            Assert.AreEqual(0x01, list[1].Version);
            Assert.AreEqual(0xfff1, list[1].Position);
        }

        [Test]
        public void query_with_end_and_start_exclusive_results_returns_items_included()
        {
            var list = _ptable.GetRange(0x0103, 0x00, 0x06).ToArray();
            Assert.AreEqual(3, list.Length);
            Assert.AreEqual(0x0103, list[0].Stream);
            Assert.AreEqual(0x05, list[0].Version);
            Assert.AreEqual(0xfff5, list[0].Position);
            Assert.AreEqual(0x0103, list[1].Stream);
            Assert.AreEqual(0x03, list[1].Version);
            Assert.AreEqual(0xfff3, list[1].Position);
            Assert.AreEqual(0x0103, list[2].Stream);
            Assert.AreEqual(0x01, list[2].Version);
            Assert.AreEqual(0xfff1, list[2].Position);
        }

        [Test]
        public void query_with_end_inside_the_hole_in_list_returns_items_included()
        {
            var list = _ptable.GetRange(0x0103, 0x00, 0x04).ToArray();
            Assert.AreEqual(2, list.Length);
            Assert.AreEqual(0x0103, list[0].Stream);
            Assert.AreEqual(0x03, list[0].Version);
            Assert.AreEqual(0xfff3, list[0].Position);
            Assert.AreEqual(0x0103, list[1].Stream);
            Assert.AreEqual(0x01, list[1].Version);
            Assert.AreEqual(0xfff1, list[1].Position);
        }

        [Test]
        public void query_with_start_inside_the_hole_in_list_returns_items_included()
        {
            var list = _ptable.GetRange(0x0103, 0x02, 0x06).ToArray();
            Assert.AreEqual(2, list.Length);
            Assert.AreEqual(0x0103, list[0].Stream);
            Assert.AreEqual(0x05, list[0].Version);
            Assert.AreEqual(0xfff5, list[0].Position);
            Assert.AreEqual(0x0103, list[1].Stream);
            Assert.AreEqual(0x03, list[1].Version);
            Assert.AreEqual(0xfff3, list[1].Position);
        }

        [Test]
        public void query_with_start_and_end_inside_the_hole_in_list_returns_items_included()
        {
            var list = _ptable.GetRange(0x0103, 0x02, 0x04).ToArray();
            Assert.AreEqual(1, list.Length);
            Assert.AreEqual(0x0103, list[0].Stream);
            Assert.AreEqual(0x03, list[0].Version);
            Assert.AreEqual(0xfff3, list[0].Position);
        }

        [Test]
        public void query_with_start_and_end_less_than_all_items_returns_nothing()
        {
            var list = _ptable.GetRange(0x0103, 0x00, 0x00).ToArray();
            Assert.AreEqual(0, list.Length);
        }

        [Test]
        public void query_with_start_and_end_greater_than_all_items_returns_nothing()
        {
            var list = _ptable.GetRange(0x0103, 0x06, 0x06).ToArray();
            Assert.AreEqual(0, list.Length);
        }
    }
}