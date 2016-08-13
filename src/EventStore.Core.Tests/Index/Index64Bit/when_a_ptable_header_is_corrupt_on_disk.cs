using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Index64Bit
{
    [TestFixture]
    public class when_a_ptable_header_is_corrupt_on_disk: Index32Bit.when_a_ptable_header_is_corrupt_on_disk
    {
        public when_a_ptable_header_is_corrupt_on_disk()
        {
            _ptableVersion = PTableVersions.Index64Bit;
        }
    }
}
