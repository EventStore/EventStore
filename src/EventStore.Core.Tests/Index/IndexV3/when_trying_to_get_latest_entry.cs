using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV3
{
    [TestFixture]
    public class when_trying_to_get_latest_entry: IndexV1.when_trying_to_get_latest_entry
    {
        public when_trying_to_get_latest_entry()
        {
            _ptableVersion = PTableVersions.IndexV3;
        }
    }
}
