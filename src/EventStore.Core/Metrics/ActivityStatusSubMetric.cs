// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Metrics;

// When there is one activity at a time and it is disposed before starting the next.
// It would be possible to drive a version of this from ActivitySource/ActivityListener
// for cases where we are already incurring the cost of allocating the Activities
public class ActivityStatusSubMetric : StatusSubMetric, IDisposable {
	private const string Idle = "Idle";

	public ActivityStatusSubMetric(string componentName, StatusMetric metric)
		: base(componentName, Idle, metric) {
	}

	public IDisposable StartActivity(string name) {
		SetStatus(name);
		return this;
	}

	public void Dispose() {
		SetStatus(Idle);
	}
}
