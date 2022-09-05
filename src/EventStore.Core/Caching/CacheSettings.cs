//qq seen
using System;
using EventStore.Common.Utils;

namespace EventStore.Core.Caching {
	public abstract class CacheSettings {
		public string Name { get; }

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

		//qq could these two be initialied in the ctor? or otherwise implemented with inheritance?
		// would be odd to need inheritance for a settings object though
		public Func<long> GetMemoryUsage { get; set; } = () => throw new InvalidOperationException("GetMemoryUsage not initialized");
		public Action<long> UpdateMaxMemoryAllocation { get; set; } = _ => throw new InvalidOperationException("UpdateMaxMemoryAllocation not initialized");

		protected CacheSettings(
			string name,
			long initialMaxMemAllocation) {

			Name = name;
			InitialMaxMemAllocation = initialMaxMemAllocation;
		}

		//qq get rid of these two methods
		public static ICacheSettings Static(string name, long memAllocation) {
			return new StaticCacheSettings(name, memAllocation);
		}

		public static ICacheSettings Dynamic(string name, long minMemAllocation, int weight) {
			return new DynamicCacheSettings(name, minMemAllocation, weight);
		}
	}

	//qq rename to not be settings i expect
	public class StaticCacheSettings : CacheSettings, ICacheSettings {
		public long _allotment;

		public StaticCacheSettings(string name, long memAllocation)
			: base(name, memAllocation) {
			Ensure.Nonnegative(memAllocation, nameof(memAllocation));
			_allotment = memAllocation;
		}

		public int Weight => 0; //qq rather get rid of weight from the interface altogether
		public long MinMemAllocation => _allotment;

		public long CalcMemAllotment(long availableMem, int totalWeight) => _allotment;
	}

	public class DynamicCacheSettings : CacheSettings, ICacheSettings {
		public DynamicCacheSettings(string name, long minMemAllocation, int weight)
			: base(name, -1) {
			Ensure.Positive(weight, nameof(weight));
			Ensure.Nonnegative(minMemAllocation, nameof(minMemAllocation));

			Weight = weight;
			MinMemAllocation = minMemAllocation;
		}

		public int Weight { get; }
		public long MinMemAllocation { get; }

		public long CalcMemAllotment(long availableMem, int totalWeight) {
			//qq maybe we should subtract the static allowances from the availableMem.
			// otherwise wont we allocate all the availableMem to dynamic caches and then allocate
			// more to the static ones
			//qq suspect this should actuall be a method on CacheSettings and cache settings should maybe be renamed.
			//qq ought to be checked by one of the DynamicCacheManagerTests

			//qq if the size calculation under-estimates, what will happen?
			//	- the cache will use up more memory than it is supposed to
			//  - if significant enough the caches will be resized downwards
			//  - that will free up more space than expected, caches might resize upwards
			//  - and repeatedly osciliate
			// if it over-estimates, what will happen?
			//  - the cache will just be smaller than it could have been
			var allocatedMem = CalcMemAllocation(availableMem, Weight, MinMemAllocation, totalWeight);
			return allocatedMem;
		}

		//qq prolly refactor into the above
		private static long CalcMemAllocation(long availableMem, int cacheWeight, long minMemAllocation, int totalWeight) {
			return Math.Max(availableMem * cacheWeight / totalWeight, minMemAllocation);
		}

	}
}
