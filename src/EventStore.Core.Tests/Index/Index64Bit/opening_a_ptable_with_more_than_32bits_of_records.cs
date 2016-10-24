using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Index64Bit
{
    [TestFixture, Explicit]
    public class opening_a_ptable_with_more_than_32bits_of_records : Index32Bit.opening_a_ptable_with_more_than_32bits_of_records
    {
        public opening_a_ptable_with_more_than_32bits_of_records()
        {
            indexEntrySize = PTable.IndexEntry64Size;
        }
    }
}