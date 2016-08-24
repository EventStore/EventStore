using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Index64Bit
{
    [TestFixture]
    public class index_map_should_detect_corruption: Index32Bit.index_map_should_detect_corruption
    {
        public index_map_should_detect_corruption()
        {
            _ptableVersion = PTableVersions.Index64Bit;
        }
    }
}
