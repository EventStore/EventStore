// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

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
