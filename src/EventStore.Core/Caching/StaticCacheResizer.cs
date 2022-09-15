using System.Collections.Generic;
using EventStore.Common.Utils;
using Serilog;

namespace EventStore.Core.Caching {
	public class StaticCacheResizer : CacheResizer, ICacheResizer {
		private readonly long _capacity;

		public StaticCacheResizer(ResizerUnit unit, long capacity, IDynamicCache cache)
			: base(unit, cache) {
			Ensure.Nonnegative(capacity, nameof(capacity));
			_capacity = capacity;
		}

		public int Weight => 0;

		public long ReservedCapacity => _capacity;

		public void CalcCapacity(long unreservedCapacity, int totalWeight) {
			Cache.Capacity = _capacity;
			Log.Debug(
				"{name} statically allotted {capacity:N0} " + Unit,
				Name, Cache.Capacity);
		}

		public IEnumerable<CacheStats> GetStats(string parentKey) {
			yield return new CacheStats(BuildStatsKey(parentKey), Name, Cache.Capacity, Size);
		}
	}
}
