// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Data;

public record ChunkInfo {
	public string ChunkLocator;
	public int ChunkStartNumber;
	public int ChunkEndNumber;
	public long ChunkStartPosition;
	public long ChunkEndPosition;
	public bool IsCompleted;
}
