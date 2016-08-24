using System;
using EventStore.Common.Log;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Index64Bit
{
    public class ptable_midpoint_cache_should: Index32Bit.ptable_midpoint_cache_should
    {
        public ptable_midpoint_cache_should()
        {
            _ptableVersion = PTableVersions.Index64Bit;
        }
    }
}
