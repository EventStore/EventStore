using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Index64Bit
{
    [TestFixture]
    public class saving_empty_index_to_a_file: Index32Bit.saving_empty_index_to_a_file
    {
        public saving_empty_index_to_a_file()
        {
            _ptableVersion = PTableVersions.Index64Bit;
        }
    }
}