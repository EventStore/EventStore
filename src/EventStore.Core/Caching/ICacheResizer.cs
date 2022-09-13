using System.Collections.Generic;

namespace EventStore.Core.Caching {
	public interface ICacheResizer {
		string Name { get; }
		int Weight { get; }
		long Size { get; }
		void CalcCapacity(long totalCapacity, int totalWeight);
		IEnumerable<ICacheStats> GetStats(string parentKey);
	}

	public interface IAllotment {
		long Capacity { get; set; }
		long Size { get; }
	}
}
