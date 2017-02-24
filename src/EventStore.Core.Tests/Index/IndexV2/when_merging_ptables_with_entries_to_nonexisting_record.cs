using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV2
{
    [TestFixture]
    public class when_merging_ptables_with_entries_to_nonexisting_record: IndexV1.when_merging_ptables_with_entries_to_nonexisting_record
    {
        public when_merging_ptables_with_entries_to_nonexisting_record()
        {
            _ptableVersion = PTableVersions.IndexV2;
        }
    }
}