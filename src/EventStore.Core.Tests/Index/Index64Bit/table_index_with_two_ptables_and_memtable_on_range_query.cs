using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Index64Bit
{
    [TestFixture, Category("LongRunning")]
    public class table_index_with_two_ptables_and_memtable_on_range_query : Index32Bit.table_index_with_two_ptables_and_memtable_on_range_query
    {
        public table_index_with_two_ptables_and_memtable_on_range_query()
        {
            _ptableVersion = PTableVersions.Index64Bit;
        }
    }
}