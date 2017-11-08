using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV2
{
    [TestFixture]
    public class when_a_ptable_is_loaded_from_disk: IndexV1.when_a_ptable_is_loaded_from_disk
    {
        public when_a_ptable_is_loaded_from_disk()
        {
            _ptableVersion = PTableVersions.IndexV2;
        }
    }
}
