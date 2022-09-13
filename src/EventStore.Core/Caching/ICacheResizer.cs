using System.Collections.Generic;

namespace EventStore.Core.Caching {
	public interface ICacheResizer {
		string Name { get; }
		int Weight { get; }
		long GetSize();
		void CalcCapacity(long totalCapacity, int totalWeight);
		IEnumerable<ICacheStats> GetStats(string parentKey);
	}

	public interface IAllotment {
		long Capacity { get; }
		long GetSize();
		void SetCapacity(long capacity);
	}
}
