using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV3
{
    [TestFixture]
    public class when_creating_ptable_from_memtable: IndexV1.when_creating_ptable_from_memtable
    {
        public when_creating_ptable_from_memtable()
        {
            _ptableVersion = PTableVersions.IndexV3;
        }
    }
}