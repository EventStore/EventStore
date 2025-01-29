// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using EventStore.Core.Time;

namespace EventStore.Core.Metrics;

// This represents an activity that can fail
public struct Duration : IDisposable {
	private readonly DurationMetric _metric;
	private readonly string _name;
	private readonly Instant _start;
	private bool _failed;

	public static Duration Nil { get; } = new();

	public Duration(DurationMetric metric, string name, Instant start) {
		_metric = metric;
		_name = name;
		_start = start;
		_failed = false;
	}

	public void SetException(Exception ex) {
		_failed = true;
	}

	public readonly void Dispose() {
		_metric?.Record(
			_start,
			new KeyValuePair<string, object>("activity", _name),
			new KeyValuePair<string, object>("status", _failed ? "failed" : "successful"));
	}
}
