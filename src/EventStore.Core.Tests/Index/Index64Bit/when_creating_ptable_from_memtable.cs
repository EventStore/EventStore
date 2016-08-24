using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Index64Bit
{
    [TestFixture]
    public class when_creating_ptable_from_memtable: Index32Bit.when_creating_ptable_from_memtable
    {
        public when_creating_ptable_from_memtable()
        {
            _ptableVersion = PTableVersions.Index64Bit;
        }
    }
}