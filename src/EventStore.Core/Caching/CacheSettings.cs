//qq seen
using System;
using EventStore.Common.Utils;

namespace EventStore.Core.Caching {
	public abstract class CacheSettings {
		public string Name { get; }

		//qq could these two be initialied in the ctor? or otherwise implemented with inheritance?
		// would be odd to need inheritance for a settings object though
		private readonly Func<long> _getMemoryUsage;
		private readonly Action<long> _updateMaxMemoryAllocation;

		protected CacheSettings(string name) {
			Name = name;
		}

		public long GetMemoryUsage() => _getMemoryUsage();
		public void UpdateMaxMemoryAllocation(long allotment) => _updateMaxMemoryAllocation(allotment);

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

		public StaticCacheSettings(string name, long memAllocation) : base(name) {
			Ensure.Nonnegative(memAllocation, nameof(memAllocation));
			_allotment = memAllocation;
		}

		public int Weight => 0; //qq rather get rid of weight from the interface altogether

		public long CalcMemAllotment(long availableMem, int totalWeight) => _allotment;
	}

	public class DynamicCacheSettings : CacheSettings, ICacheSettings {
		private readonly long _minMemAllocation;

		public DynamicCacheSettings(string name, long minMemAllocation, int weight) : base(name) {
			Ensure.Positive(weight, nameof(weight));
			Ensure.Nonnegative(minMemAllocation, nameof(minMemAllocation));

			Weight = weight;
			_minMemAllocation = minMemAllocation;
		}

		public int Weight { get; }

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
			var allocatedMem = CalcMemAllocation(availableMem, Weight, _minMemAllocation, totalWeight);
			return allocatedMem;
		}

		//qq prolly refactor into the above
		private static long CalcMemAllocation(long availableMem, int cacheWeight, long minMemAllocation, int totalWeight) {
			return Math.Max(availableMem * cacheWeight / totalWeight, minMemAllocation);
		}

	}
}
