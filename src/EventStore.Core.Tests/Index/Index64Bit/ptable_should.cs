using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Index64Bit
{
    [TestFixture]
    public class ptable_should: Index32Bit.ptable_should
    {
        public ptable_should()
        {
            _ptableVersion = PTableVersions.Index64Bit;
        }
    }
}