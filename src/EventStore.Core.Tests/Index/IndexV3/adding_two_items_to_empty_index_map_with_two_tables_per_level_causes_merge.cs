using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV3
{
    [TestFixture]
    public class adding_two_items_to_empty_index_map_with_two_tables_per_level_causes_merge: IndexV1.adding_two_items_to_empty_index_map_with_two_tables_per_level_causes_merge
    {
        public adding_two_items_to_empty_index_map_with_two_tables_per_level_causes_merge()
        {
            _ptableVersion = PTableVersions.IndexV3;
        }
    }
}