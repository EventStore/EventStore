using System;
using EventStore.Common.Utils;

namespace EventStore.Core.Caching {
	public class CacheSettings : ICacheSettings {
		public string Name { get; }
		public bool IsDynamic { get; }
		public int Weight { get; }

		private long _initialMaxMemAllocation = -1;
		public long InitialMaxMemAllocation {
			get {
				if (_initialMaxMemAllocation < 0)
					throw new InvalidOperationException("InitialMaxMemAllocation not initialized");
				return _initialMaxMemAllocation;
			}
			set => _initialMaxMemAllocation = value;
		}
		public long MinMemAllocation { get; }
		public Func<long> GetMemoryUsage { get; set; } = () => throw new InvalidOperationException("GetMemoryUsage not initialized");
		public Action<long> UpdateMaxMemoryAllocation { get; set; } = _ => throw new InvalidOperationException("UpdateMaxMemoryAllocation not initialized");

		private CacheSettings(
			string name,
			bool isDynamic,
			int weight,
			long minMemAllocation,
			long initialMaxMemAllocation) {
			Name = name;
			IsDynamic = isDynamic;
			Weight = weight;
			MinMemAllocation = minMemAllocation;
			InitialMaxMemAllocation = initialMaxMemAllocation;
		}

		public static CacheSettings Static(string name, long memAllocation) {
			Ensure.Nonnegative(memAllocation, nameof(memAllocation));
			return new CacheSettings(name, false, 0, memAllocation, memAllocation);
		}

		public static CacheSettings Dynamic(string name, long minMemAllocation, int weight) {
			Ensure.Positive(weight, nameof(weight));
			Ensure.Nonnegative(minMemAllocation, nameof(minMemAllocation));
			return new CacheSettings(name, true, weight, minMemAllocation, -1);
		}
	}
}
