using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV3
{
    [TestFixture]
    public class ptable_should: IndexV1.ptable_should
    {
        public ptable_should()
        {
            _ptableVersion = PTableVersions.IndexV3;
        }
    }
}