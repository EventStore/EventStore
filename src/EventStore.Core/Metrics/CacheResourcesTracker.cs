// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

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
