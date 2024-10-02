// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Messaging;
using EventStore.Core.Time;

namespace EventStore.Core.Bus;

public struct QueueItem {
	public QueueItem(Instant enqueuedAt, Message message) {
		EnqueuedAt = enqueuedAt;
		Message = message;
	}

	public Instant EnqueuedAt { get; }
	public Message Message { get; }
}
