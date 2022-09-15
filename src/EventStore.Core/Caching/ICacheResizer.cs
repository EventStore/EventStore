using System.Collections.Generic;

namespace EventStore.Core.Caching {
	// The resizer is responsible for resizing its allotment.
	// The resizer can be part of a hierarchy of resizers.
	// Children can _reserve_ capacity, making it unavailable to be distributed by weight.
	//     Capacity = ReservedCapacity + UnreservedCapacity
	// To trigger resizing, the parent calls the CalcCapacity method, passing in
	//   - its unreserved capacity (i.e. the capacity to be shared among the children by weight)
	//   - total weight of the children (so that they can draw the correct amount according to their weight)
	public interface ICacheResizer {
		string Name { get; }

		// How much of the capacity given to this resizer is reserved.
		// The remaining (unreserved) capacity can be allotted by weight among the children.
		long ReservedCapacity { get; }

		// Weight relative to siblings when drawing from the unreserved capacity
		int Weight { get; }

		// How much of the capacity is currently utilized
		long Size { get; }

		void CalcCapacity(long unreservedCapacity, int totalWeight);

		IEnumerable<ICacheStats> GetStats(string parentKey);
	}

	public static class CacheResizerExtensions {
		// Helper for the top level to just pass in the totalCapacity
		public static void CalcCapacityTopLevel(this ICacheResizer self, long totalCapacity) {
			self.CalcCapacity(
				unreservedCapacity: totalCapacity - self.ReservedCapacity,
				totalWeight: self.Weight);
		}
	}
}
