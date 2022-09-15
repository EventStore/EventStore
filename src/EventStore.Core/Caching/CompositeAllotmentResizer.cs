using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Utils;

namespace EventStore.Core.Caching {
	public class CompositeAllotmentResizer : AllotmentResizer, IAllotmentResizer {
		private readonly IAllotmentResizer[] _children;

		public CompositeAllotmentResizer(string name, string unit, int weight, params IAllotmentResizer[] children)
			: base(unit, new CompositeAllotment(name, children)) {
			Weight = weight;
			_children = children;
			ReservedCapacity = _children.Sum(x => x.ReservedCapacity);
		}

		public int Weight { get; }

		public long ReservedCapacity { get; }

		public void CalcCapacity(long unreservedCapacity, int totalWeight) {
			var totalCapactiy = unreservedCapacity + ReservedCapacity;
			Allotment.Capacity = totalCapactiy.ScaleByWeight(Weight, totalWeight);
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
			private readonly IAllotmentResizer[] _children;
			private readonly int _childrenWeight;
			private readonly long _reservedCapacity;

			public CompositeAllotment(string name, params IAllotmentResizer[] children) {
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
					var unreservedCapacity = Math.Max(_capacity - _reservedCapacity, 0);
					foreach (var child in _children) {
						child.CalcCapacity(unreservedCapacity, _childrenWeight);
					}
				}
			}

			public long Size => _children.Sum(static x => x.Size);
		}
	}
}
