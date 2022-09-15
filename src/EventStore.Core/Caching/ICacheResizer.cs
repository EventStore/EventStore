using System.Collections.Generic;

namespace EventStore.Core.Caching {
	public interface ICacheResizer {
		string Name { get; }
		int Weight { get; }
		long ReservedCapacity { get; }
		long Size { get; }
		void CalcCapacity(long unreservedCapacity, int totalWeight);
		IEnumerable<ICacheStats> GetStats(string parentKey);
	}

	public static class CacheResizerExtensions {
		public static void CalcCapacityTopLevel(this ICacheResizer self, long totalCapacity) {
			self.CalcCapacity(
				unreservedCapacity: totalCapacity - self.ReservedCapacity,
				totalWeight: self.Weight);
		}
	}
}
