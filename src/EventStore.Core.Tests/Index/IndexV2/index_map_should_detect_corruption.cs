using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV2
{
    [TestFixture]
    public class index_map_should_detect_corruption: IndexV1.index_map_should_detect_corruption
    {
        public index_map_should_detect_corruption()
        {
            _ptableVersion = PTableVersions.IndexV2;
        }
    }
}
