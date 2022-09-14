using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EventStore.Common.Utils;
using Serilog;

namespace EventStore.Core.Caching {
	public abstract class CacheResizer {
		public string Name => Allotment.Name;
		public long Size => Allotment.Size;
		protected IAllotment Allotment { get; }
		public string Unit { get; }

		protected CacheResizer(
			string unit,
			IAllotment allotment) {
			Ensure.NotNull(unit, nameof(unit));
			Ensure.NotNull(allotment, nameof(allotment));

			Allotment = allotment;
			Unit = unit;
		}

		protected string BuildStatsKey(string parentKey) =>
			parentKey.Length == 0 ? Name : $"{parentKey}-{Name}";

		protected static string GetParentKey(string key) {
			var index = key.LastIndexOf('-');
			return index < 0 ? null : key[..index];
		}
	}

	public class StaticCacheResizer : CacheResizer, ICacheResizer {
		private readonly long _capacity;

		public StaticCacheResizer(string unit, long capacity, IAllotment allotment)
			: base(unit, allotment) {
			Ensure.Nonnegative(capacity, nameof(capacity));
			_capacity = capacity;
		}

		public int Weight => 0;

		public long ReservedCapacity => _capacity;

		public void CalcCapacity(long totalCapacity, int totalWeight) {
			Allotment.Capacity = _capacity;
		}

		public IEnumerable<ICacheStats> GetStats(string parentKey) {
			yield return new CacheStats(BuildStatsKey(parentKey), Name, Weight, Allotment.Capacity, Size);
		}
	}

	public class DynamicCacheResizer : CacheResizer, ICacheResizer {
		private readonly long _minCapacity;

		public DynamicCacheResizer(string unit, long minCapacity, int weight, IAllotment allotment)
			: base(unit, allotment) {
			Ensure.Positive(weight, nameof(weight));
			Ensure.Nonnegative(minCapacity, nameof(minCapacity));

			Weight = weight;
			_minCapacity = minCapacity;
		}

		public int Weight { get; }

		public long ReservedCapacity => 0;

		public void CalcCapacity(long totalCapacity, int totalWeight) {
			var sw = Stopwatch.StartNew();

			var capacity = Math.Max(totalCapacity.ScaleByWeight(Weight, totalWeight), _minCapacity);
			Allotment.Capacity = capacity;

			sw.Stop();
			Log.Debug(
				"{name} cache allotted {allottedMem:N0} " + Unit + ". Took {elapsed}.",
				Name, Allotment.Capacity, sw.Elapsed);
		}

		public IEnumerable<ICacheStats> GetStats(string parentKey) {
			yield return new CacheStats(BuildStatsKey(parentKey), Name, Weight, Allotment.Capacity, Size);
		}
	}

	public class CompositeCacheResizer : CacheResizer, ICacheResizer {
		private readonly ICacheResizer[] _children;

		public CompositeCacheResizer(string name, string unit, int weight, params ICacheResizer[] children)
			: base(unit, new CompositeAllotment(name, children)) {
			Weight = weight;
			_children = children;
			ReservedCapacity = _children.Sum(x => x.ReservedCapacity);
		}

		public int Weight { get; }

		public long ReservedCapacity { get; }

		public void CalcCapacity(long totalCapacity, int totalWeight) {
			Allotment.Capacity = totalCapacity.ScaleByWeight(Weight, totalWeight);
		}

		public IEnumerable<ICacheStats> GetStats(string parentKey) {
			var key = BuildStatsKey(parentKey);
			var memUsed = 0L;

			foreach (var child in _children) {
				foreach (var childStats in child.GetStats(key)) {
					if (GetParentKey(childStats.Key) == key)
						memUsed += childStats.MemUsed;

					yield return childStats;
				}
			}

			yield return new CacheStats(key, Name, Weight, Allotment.Capacity, memUsed);
		}

		class CompositeAllotment : IAllotment {
			private readonly ICacheResizer[] _children;
			private readonly int _childrenWeight;
			private readonly long _reservedCapacity;

			public CompositeAllotment(string name, params ICacheResizer[] children) {
				Ensure.NotNullOrEmpty(name, nameof(name));
				Name = name;
				_children = children;
				_childrenWeight = children.Sum(static x => x.Weight);
				_reservedCapacity = children.Sum(static x => x.ReservedCapacity);
			}

			public string Name { get; }

			private long _capacity;
			public long Capacity {
				get => _capacity;
				set {
					_capacity = value;
					var dynamicCapacity = _capacity - _reservedCapacity;
					foreach (var child in _children) {
						var capacityAvailableToChild = Math.Max(dynamicCapacity + child.ReservedCapacity, 0);
						child.CalcCapacity(capacityAvailableToChild, _childrenWeight);
					}
				}
			}

			public long Size => _children.Sum(static x => x.Size);
		}
	}
}
