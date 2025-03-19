// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.POC.ConnectorsEngine.Processing.Checkpointing;

public record CheckpointConfig(int Interval) {
	public static CheckpointConfig None => new(0);
}
