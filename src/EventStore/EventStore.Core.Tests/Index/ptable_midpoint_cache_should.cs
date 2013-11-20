// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using EventStore.Common.Log;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    public class ptable_midpoint_cache_should: SpecificationWithDirectory
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<ptable_midpoint_cache_should>();

        [Test, Category("LongRunning"), Ignore("Veerrrryyy long running :)")]
        public void construct_valid_cache_for_any_combination_of_params()
        {
            var rnd = new Random(123987);
            for (int count = 0; count < 4096; ++count)
            {
                PTable ptable = null;
                try
                {
                    Log.Trace("Creating PTable with count {0}", count);
                    ptable = ConstructPTable(GetFilePathFor(string.Format("{0}.ptable", count)), count, rnd);

                    for (int depth = 0; depth < 15; ++depth)
                    {
                        var cache = ptable.CacheMidpoints(depth);
                        ValidateCache(cache, count, depth);
                    }
                }
                finally
                {
                    if (ptable != null)
                        ptable.MarkForDestruction();
                }
            }
        }

        private PTable ConstructPTable(string file, int count, Random rnd)
        {
            var memTable = new HashListMemTable(20000);
            for (int i = 0; i < count; ++i)
            {
                memTable.Add((uint)rnd.Next(), rnd.Next(0, 1<<20), Math.Abs(rnd.Next() * rnd.Next()));
            }

            var ptable = PTable.FromMemtable(memTable, file, 0);
            return ptable;
        }

        private void ValidateCache(PTable.Midpoint[] cache, int count, int depth)
        {
            if (count == 0 || depth == 0)
            {
                Assert.IsNull(cache);
                return;
            }

            if (count == 1)
            {
                Assert.IsNotNull(cache);
                Assert.AreEqual(2, cache.Length);
                Assert.AreEqual(0, cache[1].ItemIndex);
                Assert.AreEqual(0, cache[1].ItemIndex);
                return;
            }

            Assert.IsNotNull(cache);
            Assert.AreEqual(Math.Min(count, 1<<depth), cache.Length);

            Assert.AreEqual(0, cache[0].ItemIndex);
            for (int i = 1; i < cache.Length; ++i)
            {
                Assert.GreaterOrEqual(cache[i-1].Key, cache[i].Key);
                Assert.Less(cache[i-1].ItemIndex, cache[i].ItemIndex);
            }
            Assert.AreEqual(count-1, cache[cache.Length-1].ItemIndex);
        }
    }
}
