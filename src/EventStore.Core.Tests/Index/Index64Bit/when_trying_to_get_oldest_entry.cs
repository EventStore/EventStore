using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Index64Bit
{
    [TestFixture]
    public class when_trying_to_get_oldest_entry: Index32Bit.when_trying_to_get_oldest_entry
    {
        public when_trying_to_get_oldest_entry()
        {
            _ptableVersion = PTableVersions.Index64Bit;
        }
    }
}
