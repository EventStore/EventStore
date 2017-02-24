using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV2
{
    [TestFixture]
    public class table_index_should : IndexV1.table_index_should
    {
        public table_index_should()
        {
            _ptableVersion = PTableVersions.IndexV2;
        }
    }
}