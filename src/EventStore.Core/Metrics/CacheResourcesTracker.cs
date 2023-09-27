using System;
using EventStore.Core.Caching;

namespace EventStore.Core.Metrics;

public interface ICacheResourcesTracker {
	public void Register(string cacheKey, ResizerUnit unit, Func<CacheStats> getStats);
}

public class CacheResourcesTracker : ICacheResourcesTracker {
	private readonly CacheResourcesMetrics _metrics;

	public CacheResourcesTracker(CacheResourcesMetrics metrics) {
		_metrics = metrics;
	}

	public void Register(string cache, ResizerUnit unit, Func<CacheStats> getStats) {
		_metrics.Register(cache, unit, getStats);
	}

	public class NoOp : ICacheResourcesTracker {
		public void Register(string cacheKey, ResizerUnit unit, Func<CacheStats> getStats) {
		}
	}
}
