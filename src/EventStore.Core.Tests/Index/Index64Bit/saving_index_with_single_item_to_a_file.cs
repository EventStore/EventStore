using System;
using System.IO;
using System.Linq;
using EventStore.Core.Index;
using EventStore.Core.Util;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Index64Bit
{
    [TestFixture]
    public class saving_index_with_single_item_to_a_file: Index32Bit.saving_index_with_single_item_to_a_file
    {
        public saving_index_with_single_item_to_a_file()
        {
            _ptableVersion = PTableVersions.Index64Bit;
        }
    }
}