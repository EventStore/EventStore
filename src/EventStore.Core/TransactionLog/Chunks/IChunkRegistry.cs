// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using EventStore.Core.TransactionLog.Chunks.TFChunk;

namespace EventStore.Core.TransactionLog.Chunks;

public interface IChunkRegistry<out TChunk>
	where TChunk : class, IChunkBlob {

	TChunk TryGetChunkFor(long logPosition);

	TChunk GetChunk(int chunkNum);
}
