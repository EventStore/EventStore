using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV3
{
    [TestFixture]
    public class table_index_on_try_get_one_value_query: IndexV1.table_index_on_try_get_one_value_query
    {
        public table_index_on_try_get_one_value_query()
        {
            _ptableVersion = PTableVersions.IndexV3;
        }
    }
}