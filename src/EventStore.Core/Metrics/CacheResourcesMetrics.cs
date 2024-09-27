// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using EventStore.Core.Caching;

namespace EventStore.Core.Metrics;

public class CacheResourcesMetrics {
	private readonly ObservableUpDownMetric<long> _bytesMetric;
	private readonly ObservableUpDownMetric<long> _entriesMetric;

	public CacheResourcesMetrics(Meter meter, string name) {
		_bytesMetric = new ObservableUpDownMetric<long>(meter, name + "-bytes");
		_entriesMetric = new ObservableUpDownMetric<long>(meter, name + "-entries");
	}

	public void Register(string cache, ResizerUnit unit, Func<CacheStats> getStats) {
		var sizeAndCapacityMetric = unit == ResizerUnit.Entries
			? _entriesMetric
			: _bytesMetric;

		Register(sizeAndCapacityMetric, cache, "capacity", () => getStats().Capacity);
		Register(sizeAndCapacityMetric, cache, "size", () => getStats().Size);
		Register(_entriesMetric, cache, "count", () => getStats().Count);
	}

	private static void Register(
		ObservableUpDownMetric<long> metric,
		string cache,
		string metricName,
		Func<long> measurementProvider) {

		var tags = new KeyValuePair<string, object>[] {
			new("cache", cache),
			new("kind", metricName)
		};
		metric.Register(() => new(measurementProvider(), tags));
	}
}
