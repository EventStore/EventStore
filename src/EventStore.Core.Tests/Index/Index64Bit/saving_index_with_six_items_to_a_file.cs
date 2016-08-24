using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Index64Bit
{
    [TestFixture]
    public class saving_index_with_six_items_to_a_file: Index32Bit.saving_index_with_six_items_to_a_file
    {
        public saving_index_with_six_items_to_a_file()
        {
            _ptableVersion = PTableVersions.Index64Bit;
        }
    }
}