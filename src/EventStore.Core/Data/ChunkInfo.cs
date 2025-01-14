// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Data;

public sealed record ChunkInfo {
	public required string ChunkLocator { get; init; }
	public required int ChunkStartNumber { get; init; }
	public required int ChunkEndNumber { get; init; }
	public required long ChunkStartPosition { get; init; }
	public required long ChunkEndPosition { get; init; }
	public required bool IsCompleted { get; init; }
}
