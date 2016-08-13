using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Index64Bit
{
    [TestFixture]
    public class index_map_should: Index32Bit.index_map_should
    {
        public index_map_should()
        {
            _ptableVersion = PTableVersions.Index64Bit;
        }
    }
}