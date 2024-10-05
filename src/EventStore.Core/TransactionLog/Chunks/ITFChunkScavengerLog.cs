// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Index;

namespace EventStore.Core.TransactionLog.Chunks;

public interface ITFChunkScavengerLog : IIndexScavengerLog {
	string ScavengeId { get; }

	long SpaceSaved { get; }

	void ScavengeStarted();

	void ScavengeStarted(bool alwaysKeepScavenged, bool mergeChunks, int startFromChunk, int threads);

	void ChunksScavenged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed, long spaceSaved);

	void ChunksNotScavenged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed, string errorMessage);

	void ChunksMerged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed, long spaceSaved);

	void ChunksNotMerged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed, string errorMessage);

	void ScavengeCompleted(ScavengeResult result, string error, TimeSpan elapsed);
}

public enum ScavengeResult {
	Success,
	Stopped,
	Errored,
	Interrupted,
}

public enum LastScavengeResult {
	Unknown,
	InProgress,
	Success,
	Stopped,
	Errored,
}
