// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Bus;

/// <summary>
/// A struct providing information for <see cref="ISingleConsumerMessageQueue.TryDequeue"/> result.
/// </summary>
public struct QueueBatchDequeueResult {
	public int DequeueCount;
	public int EstimateCurrentQueueCount;
}
