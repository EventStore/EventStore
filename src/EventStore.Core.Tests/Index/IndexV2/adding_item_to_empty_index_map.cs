using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV2
{
    [TestFixture]
    public class adding_item_to_empty_index_map: IndexV1.adding_item_to_empty_index_map
    {
        public adding_item_to_empty_index_map()
        {
            _ptableVersion = PTableVersions.IndexV2;
        }
    }
}