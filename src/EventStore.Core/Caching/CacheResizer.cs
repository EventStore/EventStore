using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EventStore.Common.Utils;
using Serilog;

namespace EventStore.Core.Caching {
	public abstract class CacheResizer {
		public string Name { get; }
		protected IAllotment Allotment { get; }
		public string Unit { get; }

		protected CacheResizer(
			string name,
			string unit,
			IAllotment allotment) {
			Ensure.NotNullOrEmpty(name, nameof(name));
			Ensure.NotNull(unit, nameof(unit));
			Ensure.NotNull(allotment, nameof(allotment));

			Name = name;
			Allotment = allotment;
			Unit = unit;
		}

		protected string BuildStatsKey(string parentKey) =>
			parentKey.Length == 0 ? Name : $"{parentKey}-{Name}";

		protected static string GetParentKey(string key) {
			var index = key.LastIndexOf('-');
			return index < 0 ? null : key[..index];
		}

		protected void TimeAllotment(Action allotmentAction) {
			var sw = Stopwatch.StartNew();
			allotmentAction();
			sw.Stop();

			Log.Debug(
				"{name} cache allotted {allottedMem:N0} " + Unit + ". Took {elapsed}.",
				Name, Allotment.Capacity, sw.Elapsed);
		}
	}

	public class StaticCacheResizer : CacheResizer, ICacheResizer {
		private readonly long _capacity;
		private bool _isAllotted;

		public StaticCacheResizer(string name, string unit, long capacity, IAllotment allotment)
			: base(name, unit, allotment) {

			Ensure.Nonnegative(capacity, nameof(capacity));
			_capacity = capacity;
			_isAllotted = false;
		}

		public int Weight => 0;

		public long Size => Allotment.Size;

		public void CalcCapacity(long totalCapacity, int totalWeight) {
			if (_isAllotted)
				return;

			_isAllotted = true;
			TimeAllotment(() => Allotment.Capacity = _capacity);
		}

		public IEnumerable<ICacheStats> GetStats(string parentKey) {
			yield return new CacheStats(BuildStatsKey(parentKey), Name, Weight, Allotment.Capacity, Size);
		}
	}

	public class DynamicCacheResizer : CacheResizer, ICacheResizer {
		private readonly long _minCapacity;

		public DynamicCacheResizer(string name, string unit, long minCapacity, int weight, IAllotment allotment)
			: base(name, unit, allotment) {

			Ensure.Positive(weight, nameof(weight));
			Ensure.Nonnegative(minCapacity, nameof(minCapacity));

			Weight = weight;
			_minCapacity = minCapacity;
		}

		public int Weight { get; }

		public long Size => Allotment.Size;

		public void CalcCapacity(long totalCapacity, int totalWeight) {
			var capacity = Math.Max(totalCapacity.ScaleByWeight(Weight, totalWeight), _minCapacity);
			TimeAllotment(() => Allotment.Capacity = capacity);
		}

		public IEnumerable<ICacheStats> GetStats(string parentKey) {
			yield return new CacheStats(BuildStatsKey(parentKey), Name, Weight, Allotment.Capacity, Size);
		}
	}

	public class CompositeCacheResizer : CacheResizer, ICacheResizer {
		private readonly ICacheResizer[] _children;

		public CompositeCacheResizer(string name, string unit, int weight, params ICacheResizer[] children) :
		base(name, unit, new CompositeAllotment(children)) {
			Weight = weight;
			_children = children;
		}

		public int Weight { get; }

		public void CalcCapacity(long totalCapacity, int totalWeight) {
			TimeAllotment(() => Allotment.Capacity = totalCapacity.ScaleByWeight(Weight, totalWeight));
		}

		public long Size => Allotment.Size;

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
	}

	public class CompositeAllotment : IAllotment {
		private readonly ICacheResizer[] _children;
		private readonly int _childrenWeight;

		public CompositeAllotment(ICacheResizer[] children) {
			_children = children;
			_childrenWeight = children.Sum(static x => x.Weight);
		}

		private long _capacity;
		public long Capacity {
			get => _capacity;
			set {
				_capacity = value;
				foreach (var child in _children)
					child.CalcCapacity(_capacity, _childrenWeight);
			}
		}

		public long Size => _children.Sum(static x => x.Size);
	}
}
