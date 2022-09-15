using System.Collections.Generic;
using EventStore.Common.Utils;
using Serilog;

namespace EventStore.Core.Caching {
	public class StaticAllotmentResizer : AllotmentResizer, IAllotmentResizer {
		private readonly long _capacity;

		public StaticAllotmentResizer(ResizerUnit unit, long capacity, IAllotment allotment)
			: base(unit, allotment) {
			Ensure.Nonnegative(capacity, nameof(capacity));
			_capacity = capacity;
		}

		public int Weight => 0;

		public long ReservedCapacity => _capacity;

		public void CalcCapacity(long unreservedCapacity, int totalWeight) {
			Allotment.Capacity = _capacity;
			Log.Debug(
				"{name} statically allotted {capacity:N0} " + Unit,
				Name, Allotment.Capacity);
		}

		public IEnumerable<AllotmentStats> GetStats(string parentKey) {
			yield return new AllotmentStats(BuildStatsKey(parentKey), Name, Allotment.Capacity, Size);
		}
	}
}
