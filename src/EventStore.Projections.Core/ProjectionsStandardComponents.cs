// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Common.Options;
using EventStore.Core.Bus;

namespace EventStore.Projections.Core;

public class ProjectionsStandardComponents {
	public ProjectionsStandardComponents(
		int projectionWorkerThreadCount,
		ProjectionType runProjections,
		ISubscriber leaderOutputBus,
		IPublisher leaderOutputQueue,
		ISubscriber leaderInputBus,
		IPublisher leaderInputQueue,
		bool faultOutOfOrderProjections, int projectionCompilationTimeout, int projectionExecutionTimeout,
		int maxProjectionStateSize) {
		ProjectionWorkerThreadCount = projectionWorkerThreadCount;
		RunProjections = runProjections;
		LeaderOutputBus = leaderOutputBus;
		LeaderOutputQueue = leaderOutputQueue;
		LeaderInputQueue = leaderInputQueue;
		LeaderInputBus = leaderInputBus;
		FaultOutOfOrderProjections = faultOutOfOrderProjections;
		ProjectionCompilationTimeout = projectionCompilationTimeout;
		ProjectionExecutionTimeout = projectionExecutionTimeout;
		MaxProjectionStateSize = maxProjectionStateSize;
	}

	public int ProjectionWorkerThreadCount { get; }

	public ProjectionType RunProjections { get; }

	public ISubscriber LeaderOutputBus { get; }
	public IPublisher LeaderOutputQueue { get; }

	public IPublisher LeaderInputQueue { get; }
	public ISubscriber LeaderInputBus { get; }

	public bool FaultOutOfOrderProjections { get; }

	public int ProjectionCompilationTimeout { get; }

	public int ProjectionExecutionTimeout { get; }

	public int MaxProjectionStateSize { get; }
}
