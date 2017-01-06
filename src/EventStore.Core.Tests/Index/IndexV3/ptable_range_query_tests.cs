using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV3
{
    [TestFixture]
    public class ptable_range_query_tests: IndexV1.ptable_range_query_tests
    {
        public ptable_range_query_tests()
        {
            _ptableVersion = PTableVersions.IndexV3;
        }
    }
}