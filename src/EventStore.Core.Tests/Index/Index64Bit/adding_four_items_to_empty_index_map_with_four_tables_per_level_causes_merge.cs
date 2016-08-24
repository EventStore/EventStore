using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Index64Bit
{
    [TestFixture]
    public class adding_four_items_to_empty_index_map_with_four_tables_per_level_causes_merge: Index32Bit.adding_four_items_to_empty_index_map_with_four_tables_per_level_causes_merge
    {
        public adding_four_items_to_empty_index_map_with_four_tables_per_level_causes_merge()
        {
            _ptableVersion = PTableVersions.Index64Bit;
        }
    }
}