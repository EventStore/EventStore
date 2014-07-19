using System.Linq;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class searching_ptable_with_usual_items_and_all_items_in_cache : ptable_read_scenario_with_usual_items
    {
        public searching_ptable_with_usual_items_and_all_items_in_cache()
            : base(midpointCacheDepth: 10)
        {

        }
    }

    [TestFixture]
    public class searching_ptable_with_usual_items_and_only_some_items_in_cache : ptable_read_scenario_with_usual_items
    {
        public searching_ptable_with_usual_items_and_only_some_items_in_cache()
            : base(midpointCacheDepth: 0)
        {
        }
    }
    
    [TestFixture]
    public abstract class ptable_read_scenario_with_usual_items : PTableReadScenario
    {
        protected ptable_read_scenario_with_usual_items(int midpointCacheDepth)
                : base(midpointCacheDepth)
        {

        }

        protected override void AddItemsForScenario(IMemTable memTable)
        {
            memTable.Add(0x0101, 0x0001, 0x0001);
            memTable.Add(0x0105, 0x0001, 0x0002);
            memTable.Add(0x0102, 0x0001, 0x0003);
            memTable.Add(0x0102, 0x0002, 0x0004);
            memTable.Add(0x0103, 0x0001, 0x0005);
        }

        [Test]
        public void the_table_has_five_items()
        {
            Assert.AreEqual(5, PTable.Count);
        }

        [Test]
        public void the_first_item_can_be_found()
        {
            long position;
            Assert.IsTrue(PTable.TryGetOneValue(0x0101, 0x0001, out position));
            Assert.AreEqual(0x0001, position);
        }

        [Test]
        public void the_second_item_can_be_found()
        {
            long position;
            Assert.IsTrue(PTable.TryGetOneValue(0x0102, 0x0001, out position));
            Assert.AreEqual(0x0003, position);
        }

        [Test]
        public void the_third_item_can_be_found()
        {
            long position;
            Assert.IsTrue(PTable.TryGetOneValue(0x0102, 0x0002, out position));
            Assert.AreEqual(0x0004, position);
        }

        [Test]
        public void the_fourth_item_can_be_found()
        {
            long position;
            Assert.IsTrue(PTable.TryGetOneValue(0x0102, 0x0002, out position));
            Assert.AreEqual(0x0004, position);
        }


        [Test]
        public void the_fifth_item_can_be_found()
        {
            long position;
            Assert.IsTrue(PTable.TryGetOneValue(0x0105, 0x0001, out position));
            Assert.AreEqual(0x0002, position);
        }

        [Test]
        public void non_existent_item_cannot_be_found()
        {
            long position;
            Assert.IsFalse(PTable.TryGetOneValue(0x0106, 0x0001, out position));
        }

        [Test]
        public void range_query_returns_correct_items()
        {
            // for now events are returned in order from larger key to lower
            var items = PTable.GetRange(0x0102, 0x0000, 0x0010).ToArray();
            Assert.That(items.Length, Is.EqualTo(2));
            Assert.That(items[1].Stream, Is.EqualTo(0x0102));
            Assert.That(items[1].Version, Is.EqualTo(0x0001));
            Assert.That(items[1].Position, Is.EqualTo(0x0003));
            Assert.That(items[0].Stream, Is.EqualTo(0x0102));
            Assert.That(items[0].Version, Is.EqualTo(0x0002));
            Assert.That(items[0].Position, Is.EqualTo(0x0004));
        }

        [Test]
        public void range_query_returns_correct_item1()
        {
            var items = PTable.GetRange(0x0102, 0x0000, 0x0001).ToArray();
            Assert.That(items.Length, Is.EqualTo(1));
            Assert.That(items[0].Stream, Is.EqualTo(0x0102));
            Assert.That(items[0].Version, Is.EqualTo(0x0001));
            Assert.That(items[0].Position, Is.EqualTo(0x0003));
        }

        [Test]
        public void range_query_returns_correct_item2()
        {
            var items = PTable.GetRange(0x0102, 0x0002, 0x0010).ToArray();
            Assert.That(items.Length, Is.EqualTo(1));
            Assert.That(items[0].Stream, Is.EqualTo(0x0102));
            Assert.That(items[0].Version, Is.EqualTo(0x0002));
            Assert.That(items[0].Position, Is.EqualTo(0x0004));
        }

        [Test]
        public void range_query_returns_no_items_when_no_stream_in_sstable()
        {
            var items = PTable.GetRange(0x0104, 0x0000, 0x0010);
            Assert.That(items.Count(), Is.EqualTo(0));
        }

        [Test]
        public void range_query_returns_items_when_startkey_is_less_than_current_min()
        {
            var items = PTable.GetRange(0x0101, 0x0000, 0x0010).ToArray();
            Assert.That(items.Length, Is.EqualTo(1));
            Assert.That(items[0].Stream, Is.EqualTo(0x0101));
            Assert.That(items[0].Version, Is.EqualTo(0x0001));
            Assert.That(items[0].Position, Is.EqualTo(0x0001));
        }

        [Test]
        public void range_query_returns_items_when_endkey_is_greater_than_current_max()
        {
            var items = PTable.GetRange(0x0105, 0x0000, 0x0010).ToArray();
            Assert.That(items.Length, Is.EqualTo(1));
            Assert.That(items[0].Stream, Is.EqualTo(0x0105));
            Assert.That(items[0].Version, Is.EqualTo(0x0001));
            Assert.That(items[0].Position, Is.EqualTo(0x0002));
        }
    }
}