using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV2
{
    [TestFixture]
    public class when_merging_four_ptables: IndexV1.when_merging_four_ptables
    {
        public when_merging_four_ptables()
        {
            _ptableVersion = PTableVersions.IndexV2;
        }
    }
}