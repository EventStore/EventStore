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
			Ensure.NotNull(name, nameof(name));
			Ensure.NotNull(unit, nameof(unit));
			Ensure.NotNull(allotment, nameof(allotment));

			Name = name;
			Allotment = allotment;
			Unit = unit;
		}

		protected string BuildStatsKey(string parentKey) =>
			parentKey.Length == 0 ? Name : $"{parentKey}-{Name}";

		protected void TimeAllotment(Action allotmentAction) {
			var sw = Stopwatch.StartNew();
			allotmentAction();
			sw.Stop();

			if (Name != string.Empty)
				Log.Debug(
					"{name} cache allotted {allottedMem:N0} " + Unit + ". Took {elapsed}.",
					Name, Allotment.Current, sw.Elapsed);
		}
	}

	public class StaticCacheResizer : CacheResizer, ICacheResizer {
		private readonly long _allotment;
		private bool _isAllotted;

		public StaticCacheResizer(string name, string unit, long memAllotted, IAllotment allotment)
			: base(name, unit, allotment) {

			Ensure.Nonnegative(memAllotted, nameof(memAllotted));
			_allotment = memAllotted;
			_isAllotted = false;
		}

		public int Weight => 0;

		public long GetMemUsage() => Allotment.GetUsage();

		public void CalcAllotment(long availableMem, int totalWeight) {
			if (_isAllotted)
				return;

			_isAllotted = true;
			TimeAllotment(() => Allotment.Update(_allotment));
		}

		public IEnumerable<ICacheStats> GetStats(string parentKey) {
			yield return new CacheStats(BuildStatsKey(parentKey), Name, Weight, Allotment.Current, GetMemUsage());
		}
	}

	public class DynamicCacheResizer : CacheResizer, ICacheResizer {
		private readonly long _minMemAllotment;

		public DynamicCacheResizer(string name, string unit, long minMemAllotted, int weight, IAllotment allotment)
			: base(name, unit, allotment) {

			Ensure.Positive(weight, nameof(weight));
			Ensure.Nonnegative(minMemAllotted, nameof(minMemAllotted));

			Weight = weight;
			_minMemAllotment = minMemAllotted;
		}

		public int Weight { get; }

		public long GetMemUsage() => Allotment.GetUsage();

		public void CalcAllotment(long availableMem, int totalWeight) {
			var allotment = Math.Max(availableMem.ScaleByWeight(Weight, totalWeight), _minMemAllotment);
			TimeAllotment(() => Allotment.Update(allotment));
		}

		public IEnumerable<ICacheStats> GetStats(string parentKey) {
			yield return new CacheStats(BuildStatsKey(parentKey), Name, Weight, Allotment.Current, GetMemUsage());
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

		public void CalcAllotment(long availableMem, int totalWeight) {
			TimeAllotment(() => Allotment.Update(availableMem.ScaleByWeight(Weight, totalWeight)));
		}

		public long GetMemUsage() => Allotment.GetUsage();

		public IEnumerable<ICacheStats> GetStats(string parentKey) {
			var key = BuildStatsKey(parentKey);
			var memUsed = 0L;

			foreach (var child in _children) {
				foreach (var childStats in child.GetStats(key)) {
					memUsed += childStats.MemUsed;
					yield return childStats;
				}
			}

			yield return new CacheStats(key, Name, Weight, Allotment.Current, memUsed);
		}
	}

	public class EmptyAllotment : IAllotment {
		public static EmptyAllotment Instance { get; } = new();
		public long Current { get; private set; }
		public long GetUsage() => 0;

		public void Update(long allotment) {
			Current = allotment;
		}
	}

	public class AdHocAllotment : IAllotment {
		private readonly Func<long> _getMemUsage;
		private readonly Action<long> _updateMemAllotment;

		public AdHocAllotment(Func<long> getMemUsage, Action<long> updateMemAllotment) {
			_getMemUsage = getMemUsage;
			_updateMemAllotment = updateMemAllotment;
		}

		public long Current { get; private set; }

		public long GetUsage() => _getMemUsage();

		public void Update(long allotment) {
			Current = allotment;
			_updateMemAllotment(allotment);
		}
	}

	public class CompositeAllotment : IAllotment {
		private readonly ICacheResizer[] _children;
		private readonly int _childrenWeight;

		public CompositeAllotment(ICacheResizer[] children) {
			_children = children;
			_childrenWeight = children.Sum(static x => x.Weight);
		}

		public long Current { get; private set; }
		public long GetUsage() => _children.Sum(static x => x.GetMemUsage());

		public void Update(long allotment) {
			Current = allotment;
			foreach (var child in _children)
				child.CalcAllotment(allotment, _childrenWeight);
		}
	}
}
