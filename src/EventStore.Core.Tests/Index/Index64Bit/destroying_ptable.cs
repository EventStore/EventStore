using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Index64Bit
{
    [TestFixture]
    public class destroying_ptable: Index32Bit.destroying_ptable
    {
        public destroying_ptable()
        {
            _ptableVersion = PTableVersions.Index64Bit;
        }
    }
}