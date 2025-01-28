// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.TransactionLog.Chunks.TFChunk;

namespace EventStore.Core.TransactionLog.Chunks;

public interface IChunkRegistry<out TChunk>
	where TChunk : class, IChunkBlob {

	TChunk TryGetChunkFor(long logPosition);

	TChunk GetChunk(int chunkNum);
}
