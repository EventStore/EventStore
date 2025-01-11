// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using EventStore.Core.Time;

namespace EventStore.Core.Metrics;

// This represents an activity that can fail
public struct Duration(DurationMetric metric, string name, Instant start) : IDisposable {
	private bool _failed = false;

	public static Duration Nil { get; } = new();

	public void SetException(Exception ex) {
		_failed = true;
	}

	public void Dispose() {
		metric?.Record(start, new("activity", name), new("status", _failed ? "failed" : "successful"));
	}
}
