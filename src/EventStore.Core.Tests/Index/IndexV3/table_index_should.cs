using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV3
{
    [TestFixture]
    public class table_index_should : IndexV1.table_index_should
    {
        public table_index_should()
        {
            _ptableVersion = PTableVersions.IndexV3;
        }
    }
}