using System;
using EventStore.Common.Log;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV2
{
    public class ptable_midpoint_cache_should: IndexV1.ptable_midpoint_cache_should
    {
        public ptable_midpoint_cache_should()
        {
            _ptableVersion = PTableVersions.IndexV2;
        }
    }
}
