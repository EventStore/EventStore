using System.Collections.Generic;
using EventStore.Common.Utils;

namespace EventStore.Core.Caching {
	public class StaticAllotmentResizer : AllotmentResizer, IAllotmentResizer {
		private readonly long _capacity;

		public StaticAllotmentResizer(string unit, long capacity, IAllotment allotment)
			: base(unit, allotment) {
			Ensure.Nonnegative(capacity, nameof(capacity));
			_capacity = capacity;
		}

		public int Weight => 0;

		public long ReservedCapacity => _capacity;

		public void CalcCapacity(long unreservedCapacity, int totalWeight) {
			Allotment.Capacity = _capacity;
		}

		public IEnumerable<ICacheStats> GetStats(string parentKey) {
			yield return new CacheStats(BuildStatsKey(parentKey), Name, Weight, Allotment.Capacity, Size);
		}
	}
}
