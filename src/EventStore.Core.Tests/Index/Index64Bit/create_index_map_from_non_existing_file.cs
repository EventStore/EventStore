using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Index64Bit
{
    [TestFixture]
    public class create_index_map_from_non_existing_file : Index32Bit.create_index_map_from_non_existing_file
    {
        public create_index_map_from_non_existing_file()
        {
            _ptableVersion = PTableVersions.Index64Bit;
        }
    }
}
