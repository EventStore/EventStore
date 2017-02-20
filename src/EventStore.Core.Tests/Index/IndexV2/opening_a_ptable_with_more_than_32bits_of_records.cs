using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV2
{
    [TestFixture, Explicit]
    public class opening_a_ptable_with_more_than_32bits_of_records : IndexV1.opening_a_ptable_with_more_than_32bits_of_records
    {
        public opening_a_ptable_with_more_than_32bits_of_records()
        {
            indexEntrySize = PTable.IndexEntryV2Size;
        }
    }
}