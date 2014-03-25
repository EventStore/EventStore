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
