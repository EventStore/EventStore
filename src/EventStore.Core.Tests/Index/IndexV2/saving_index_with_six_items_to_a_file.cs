using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV2
{
    [TestFixture]
    public class saving_index_with_six_items_to_a_file: IndexV1.saving_index_with_six_items_to_a_file
    {
        public saving_index_with_six_items_to_a_file()
        {
            _ptableVersion = PTableVersions.IndexV2;
        }
    }
}