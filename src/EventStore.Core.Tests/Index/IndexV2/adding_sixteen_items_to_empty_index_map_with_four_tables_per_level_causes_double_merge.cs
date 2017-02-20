using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV2
{
    [TestFixture]
    public class adding_sixteen_items_to_empty_index_map_with_four_tables_per_level_causes_double_merge: IndexV1.adding_sixteen_items_to_empty_index_map_with_four_tables_per_level_causes_double_merge
    {
        public adding_sixteen_items_to_empty_index_map_with_four_tables_per_level_causes_double_merge()
        {
            _ptableVersion = PTableVersions.IndexV2;
        }
    }
}