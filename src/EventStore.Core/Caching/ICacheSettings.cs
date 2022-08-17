using System;

namespace EventStore.Core.Caching {
	public interface ICacheSettings {
		string Name { get; }
		bool IsDynamic { get; }
		int Weight { get; }
		long InitialMaxMemAllocation { get; set; }
		long MinMemAllocation { get; }
		Func<long> GetMemoryUsage { get; set; }
		Action<long> UpdateMaxMemoryAllocation { get; set; }
	}
}
