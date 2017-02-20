using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV3
{
    [TestFixture, Category("LongRunning")]
    public class table_index_with_two_ptables_and_memtable_on_range_query : IndexV1.table_index_with_two_ptables_and_memtable_on_range_query
    {
        public table_index_with_two_ptables_and_memtable_on_range_query()
        {
            _ptableVersion = PTableVersions.IndexV3;
        }
    }
}