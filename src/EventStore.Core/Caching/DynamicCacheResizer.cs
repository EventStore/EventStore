using System;
using System.Collections.Generic;
using System.Diagnostics;
using EventStore.Common.Utils;
using Serilog;

namespace EventStore.Core.Caching {
	public class DynamicCacheResizer : CacheResizer, ICacheResizer {
		private readonly long _minCapacity;

		public DynamicCacheResizer(ResizerUnit unit, long minCapacity, int weight, IDynamicCache cache)
			: base(unit, cache) {
			Ensure.Positive(weight, nameof(weight));
			Ensure.Nonnegative(minCapacity, nameof(minCapacity));

			Weight = weight;
			_minCapacity = minCapacity;
		}

		public int Weight { get; }

		public long ReservedCapacity => 0;

		public void CalcCapacity(long unreservedCapacity, int totalWeight) {
			var sw = Stopwatch.StartNew();

			var capacity = Math.Max(unreservedCapacity.ScaleByWeight(Weight, totalWeight), _minCapacity);
			Cache.SetCapacity(capacity);

			sw.Stop();
			Log.Debug(
				"{name} dynamically allotted {capacity:N0} " + Unit + ". Took {elapsed}.",
				Name, Cache.Capacity, sw.Elapsed);
		}

		public void ResetFreedSize() {
			Cache.ResetFreedSize();
		}

		public IEnumerable<CacheStats> GetStats(string parentKey) {
			yield return new CacheStats(BuildStatsKey(parentKey), Name, Cache.Capacity, Size);
		}
	}
}
