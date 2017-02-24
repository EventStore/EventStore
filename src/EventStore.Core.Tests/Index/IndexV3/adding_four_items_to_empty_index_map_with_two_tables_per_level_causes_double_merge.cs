using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV3
{
    [TestFixture]
    public class adding_four_items_to_empty_index_map_with_two_tables_per_level_causes_double_merge: IndexV1.adding_four_items_to_empty_index_map_with_two_tables_per_level_causes_double_merge
    {
        public adding_four_items_to_empty_index_map_with_two_tables_per_level_causes_double_merge()
        {
            _ptableVersion = PTableVersions.IndexV3;
        }
    }
}