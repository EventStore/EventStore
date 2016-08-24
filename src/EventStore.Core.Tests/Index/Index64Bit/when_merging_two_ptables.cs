using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Index64Bit
{
    [TestFixture]
    public class when_merging_two_ptables: Index32Bit.when_merging_two_ptables
    {
        public when_merging_two_ptables()
        {
            _ptableVersion = PTableVersions.Index64Bit;
        }
    }
}