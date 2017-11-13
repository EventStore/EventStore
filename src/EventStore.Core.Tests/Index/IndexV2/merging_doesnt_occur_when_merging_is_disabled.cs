using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV2
{
    [TestFixture]
    public class merging_doesnt_occur_when_merging_is_disabled : IndexV1.merging_doesnt_occur_when_merging_is_disabled
    {
        public merging_doesnt_occur_when_merging_is_disabled()
        {
            _ptableVersion = PTableVersions.IndexV2;
        }
    }
}