using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV1
{
    public abstract class PTableReadScenario: SpecificationWithFile
    {
        private readonly int _midpointCacheDepth;
        protected byte _ptableVersion = PTableVersions.IndexV1;

        protected PTable PTable;

        protected PTableReadScenario(int midpointCacheDepth)
        {
            _midpointCacheDepth = midpointCacheDepth;
        }

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            var table = new HashListMemTable(_ptableVersion, maxSize: 50);

            AddItemsForScenario(table);

            PTable = PTable.FromMemtable(table, Filename, cacheDepth: _midpointCacheDepth);
        }

        [TearDown]
        public override void TearDown()
        {
            PTable.Dispose();
            
            base.TearDown();
        }

        protected abstract void AddItemsForScenario(IMemTable memTable);
    }
}