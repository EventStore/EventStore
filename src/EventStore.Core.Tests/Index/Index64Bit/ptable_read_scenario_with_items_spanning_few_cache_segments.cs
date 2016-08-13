using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Index64Bit
{
    [TestFixture]
    public class searching_ptable_with_items_spanning_few_cache_segments_and_all_items_in_cache : Index32Bit.searching_ptable_with_items_spanning_few_cache_segments_and_all_items_in_cache
    {
        public searching_ptable_with_items_spanning_few_cache_segments_and_all_items_in_cache()
        {
            _ptableVersion = PTableVersions.Index64Bit;
        }
    }

    [TestFixture]
    public class searching_ptable_with_items_spanning_few_cache_segments_and_only_some_items_in_cache : Index32Bit.searching_ptable_with_items_spanning_few_cache_segments_and_only_some_items_in_cache
    {
        public searching_ptable_with_items_spanning_few_cache_segments_and_only_some_items_in_cache()
        {
            _ptableVersion = PTableVersions.Index64Bit;
        }
    }
}
