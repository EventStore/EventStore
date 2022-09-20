using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Utils;

namespace EventStore.Core.Caching {
	public class CompositeCacheResizer : CacheResizer, ICacheResizer {
		private readonly ICacheResizer[] _children;

		public CompositeCacheResizer(string name, int weight, params ICacheResizer[] children)
			: base(GetUniqueUnit(children), new CompositeCache(name, children)) {
			Weight = weight;
			_children = children;
			ReservedCapacity = _children.Sum(x => x.ReservedCapacity);
		}

		public int Weight { get; }

		public long ReservedCapacity { get; }

		public void CalcCapacity(long unreservedCapacity, int totalWeight) {
			var totalCapacity = unreservedCapacity + ReservedCapacity;
			Cache.SetCapacity(totalCapacity.ScaleByWeight(Weight, totalWeight));
		}

		public IEnumerable<CacheStats> GetStats(string parentKey) {
			var key = BuildStatsKey(parentKey);
			var size = 0L;

			foreach (var child in _children) {
				foreach (var childStats in child.GetStats(key)) {
					if (GetParentKey(childStats.Key) == key)
						size += childStats.Size;

					yield return childStats;
				}
			}

			yield return new CacheStats(key, Name, Cache.Capacity, size);
		}

		private static ResizerUnit GetUniqueUnit(ICacheResizer[] children) {
			if (children.Length < 1)
				throw new ArgumentException("There must be at least one child", nameof(children));

			var unit = children[0].Unit;

			if (children.Any(x => x.Unit != unit))
				throw new ArgumentException("All children must have the same unit", nameof(children));

			return unit;
		}

		class CompositeCache : IDynamicCache {
			private readonly ICacheResizer[] _children;
			private readonly int _childrenWeight;
			private readonly long _reservedCapacity;

			public CompositeCache(string name, params ICacheResizer[] children) {
				Ensure.NotNullOrEmpty(name, nameof(name));
				Name = name;
				_children = children;
				_childrenWeight = children.Sum(static x => x.Weight);
				_reservedCapacity = children.Sum(static x => x.ReservedCapacity);
			}

			public string Name { get; }

			public long Capacity { get; private set; }

			public void SetCapacity(long value) {
				Capacity = value;
				var unreservedCapacity = Math.Max(Capacity - _reservedCapacity, 0);
				foreach (var child in _children) {
					child.CalcCapacity(unreservedCapacity, _childrenWeight);
				}
			}

			public long Size => _children.Sum(static x => x.Size);
		}
	}
}
