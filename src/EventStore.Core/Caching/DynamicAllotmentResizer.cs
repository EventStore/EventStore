using System;
using System.Collections.Generic;
using System.Diagnostics;
using EventStore.Common.Utils;
using Serilog;

namespace EventStore.Core.Caching {
	public class DynamicAllotmentResizer : AllotmentResizer, IAllotmentResizer {
		private readonly long _minCapacity;

		public DynamicAllotmentResizer(ResizerUnit unit, long minCapacity, int weight, IAllotment allotment)
			: base(unit, allotment) {
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
			Allotment.Capacity = capacity;

			sw.Stop();
			Log.Debug(
				"{name} dynamically allotted {capacity:N0} " + Unit + ". Took {elapsed}.",
				Name, Allotment.Capacity, sw.Elapsed);
		}

		public IEnumerable<AllotmentStats> GetStats(string parentKey) {
			yield return new AllotmentStats(BuildStatsKey(parentKey), Name, Allotment.Capacity, Size);
		}
	}
}
