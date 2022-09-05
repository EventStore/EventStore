//qq seen
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
					//qq its initialised in the constructor, so perhaps the check should be when it is set
					throw new InvalidOperationException("InitialMaxMemAllocation not initialized");
				return _initialMaxMemAllocation;
			}
			//qq do we need the set now that it is set in the ctor
			//qqqq ah but for dynamic it is set to -1 in the ctor
			set => _initialMaxMemAllocation = value;
		}
		public long MinMemAllocation { get; }

		//qq could these two be initialied in the ctor? or otherwise implemented with inheritance?
		// would be odd to need inheritance for a settings object though
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
			return new CacheSettings(
				name: name,
				isDynamic: false,
				weight: 0,
				minMemAllocation: memAllocation,
				initialMaxMemAllocation: memAllocation);
		}

		public static CacheSettings Dynamic(string name, long minMemAllocation, int weight) {
			Ensure.Positive(weight, nameof(weight));
			Ensure.Nonnegative(minMemAllocation, nameof(minMemAllocation));
			return new CacheSettings(
				name: name,
				isDynamic: true,
				weight: weight,
				minMemAllocation: minMemAllocation,
				initialMaxMemAllocation: -1);
		}
	}
}
