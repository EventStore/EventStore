using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Index64Bit
{
    [TestFixture]
    public class adding_item_to_empty_index_map: Index32Bit.adding_item_to_empty_index_map
    {
        public adding_item_to_empty_index_map()
        {
            _ptableVersion = PTableVersions.Index64Bit;
        }
    }
}