using System;
using System.Linq;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class table_index_on_range_query  :SpecificationWithDirectoryPerTestFixture
    {
        private TableIndex _tableIndex;

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            _tableIndex = new TableIndex(PathName,
                                         () => new HashListMemTable(maxSize: 40),
                                         () => { throw new InvalidOperationException(); },
                                         maxSizeForMemory: 20);
            _tableIndex.Initialize(long.MaxValue);

            _tableIndex.Add(0, 0xDEAD, 0, 0xFF00);
            _tableIndex.Add(0, 0xDEAD, 1, 0xFF01); 
                            
            _tableIndex.Add(0, 0xBEEF, 0, 0xFF00);
            _tableIndex.Add(0, 0xBEEF, 1, 0xFF01); 
                             
            _tableIndex.Add(0, 0xABBA, 0, 0xFF00);
            _tableIndex.Add(0, 0xABBA, 1, 0xFF01); 
            _tableIndex.Add(0, 0xABBA, 2, 0xFF02);
            _tableIndex.Add(0, 0xABBA, 3, 0xFF03); 
                             
            _tableIndex.Add(0, 0xDEAD, 0, 0xFF10);
            _tableIndex.Add(0, 0xDEAD, 1, 0xFF11); 
                             
            _tableIndex.Add(0, 0xADA, 0, 0xFF00);
        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            _tableIndex.Close();
            base.TestFixtureTearDown();
        }

        [Test]
        public void should_return_empty_collection_when_stream_is_not_in_db()
        {
            var res = _tableIndex.GetRange(0xFEED, 0, 100);
            Assert.That(res, Is.Empty);
        }

        [Test]
        public void should_return_all_applicable_elements_in_correct_order()
        {
            var res = _tableIndex.GetRange(0xBEEF, 0, 100).ToList();
            Assert.That(res.Count(), Is.EqualTo(2));
            Assert.That(res[0].Stream, Is.EqualTo(0xBEEF));
            Assert.That(res[0].Version, Is.EqualTo(1));
            Assert.That(res[0].Position, Is.EqualTo(0xFF01));
            Assert.That(res[1].Stream, Is.EqualTo(0xBEEF));
            Assert.That(res[1].Version, Is.EqualTo(0));
            Assert.That(res[1].Position, Is.EqualTo(0xFF00));
        }

        [Test]
        public void should_return_all_elements_with_hash_collisions_in_correct_order()
        {
            var res = _tableIndex.GetRange(0xDEAD, 0, 100).ToList();
            Assert.That(res.Count(), Is.EqualTo(4));
            Assert.That(res[0].Stream, Is.EqualTo(0xDEAD));
            Assert.That(res[0].Version, Is.EqualTo(1));
            Assert.That(res[0].Position, Is.EqualTo(0xFF11));
        
            Assert.That(res[1].Stream, Is.EqualTo(0xDEAD));
            Assert.That(res[1].Version, Is.EqualTo(1));
            Assert.That(res[1].Position, Is.EqualTo(0xFF01));

            Assert.That(res[2].Stream, Is.EqualTo(0xDEAD));
            Assert.That(res[2].Version, Is.EqualTo(0));
            Assert.That(res[2].Position, Is.EqualTo(0xFF10));
            
            Assert.That(res[3].Stream, Is.EqualTo(0xDEAD));
            Assert.That(res[3].Version, Is.EqualTo(0));
            Assert.That(res[3].Position, Is.EqualTo(0xFF00));
        }
    }
}
