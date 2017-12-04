using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV3
{
    [TestFixture(0,0)]
    [TestFixture(10,0)]
    [TestFixture(0,10)]
    [TestFixture(10,10)]
    public class table_index_hash_collision_when_upgrading_to_64bit : IndexV2.table_index_hash_collision_when_upgrading_to_64bit
    {
        public table_index_hash_collision_when_upgrading_to_64bit(int extraStreamHashesAtBeginning, int extraStreamHashesAtEnd) : base(extraStreamHashesAtBeginning, extraStreamHashesAtEnd)
        {
            _ptableVersion = PTableVersions.IndexV3;
        }
    }
}