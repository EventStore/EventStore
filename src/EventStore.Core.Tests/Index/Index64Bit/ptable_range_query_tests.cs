using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Index64Bit
{
    [TestFixture]
    public class ptable_range_query_tests: Index32Bit.ptable_range_query_tests
    {
        public ptable_range_query_tests()
        {
            _ptableVersion = PTableVersions.Index64Bit;
        }
    }
}