using System;
using static EventStore.Common.Configuration.MetricsConfiguration;

namespace EventStore.Core.Metrics;

public interface ICacheHitsMissesTracker {
	void Register(Cache cache, Func<long> getHits, Func<long> getMisses);
}

public class CacheHitsMissesTracker : ICacheHitsMissesTracker {
	private readonly CacheHitsMissesMetric _metric;

	public CacheHitsMissesTracker(CacheHitsMissesMetric metric) {
		_metric = metric;
	}

	public void Register(Cache cache, Func<long> getHits, Func<long> getMisses) =>
		_metric.Register(cache, getHits, getMisses);

	public class NoOp : ICacheHitsMissesTracker {
		public void Register(Cache cache, Func<long> getHits, Func<long> getMisses) {
		}
	}
}
