using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Index64Bit
{
    [TestFixture]
    public class table_index_should : Index32Bit.table_index_should
    {
        public table_index_should()
        {
            _ptableVersion = PTableVersions.Index64Bit;
        }
    }
}