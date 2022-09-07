using System.Collections.Generic;

namespace EventStore.Core.Caching {
	public interface ICacheResizer {
		string Name { get; }
		int Weight { get; }
		long GetMemUsage();
		void CalcAllotment(long availableMem, int totalWeight);
		IEnumerable<ICacheStats> GetStats(string parentKey);
	}

	public interface IAllotment {
		long Current { get; }
		long GetUsage();
		void Update(long allotment);
	}
}
