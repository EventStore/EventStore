using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV3
{
    [TestFixture]
    public class when_a_ptable_is_corrupt_on_disk: IndexV1.when_a_ptable_is_corrupt_on_disk
    {
        public when_a_ptable_is_corrupt_on_disk()
        {
            _ptableVersion = PTableVersions.IndexV3;
        }
    }
}
