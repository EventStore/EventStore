using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Index64Bit
{
    [TestFixture]
    public class when_trying_to_get_latest_entry: Index32Bit.when_trying_to_get_latest_entry
    {
        public when_trying_to_get_latest_entry()
        {
            _ptableVersion = PTableVersions.Index64Bit;
        }
    }
}
