using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV2
{
    [TestFixture]
    public class index_map_should: IndexV1.index_map_should
    {
        public index_map_should()
        {
            _ptableVersion = PTableVersions.IndexV2;
        }
    }
}