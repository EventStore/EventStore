// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Metrics;

public interface IProjectionTracker {
	void OnNewStats(ProjectionStatistics[] newStats);

	public static IProjectionTracker NoOp => NoOpTracker.Instance;
}

file sealed class NoOpTracker : IProjectionTracker {
	public static NoOpTracker Instance { get; } = new();

	public void OnNewStats(ProjectionStatistics[] newStats) { }
}
