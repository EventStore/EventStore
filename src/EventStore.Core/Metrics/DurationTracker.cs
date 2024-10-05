// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Metrics;


public interface IDurationTracker {
	Duration Start();
}

public class DurationTracker : IDurationTracker {
	private readonly DurationMetric _metric;
	private readonly string _durationName;

	public DurationTracker(DurationMetric metric, string durationName) {
		_metric = metric;
		_durationName = durationName;
	}

	public Duration Start() => _metric.Start(_durationName);

	public class NoOp : IDurationTracker {
		public Duration Start() => Duration.Nil;
	}
}
