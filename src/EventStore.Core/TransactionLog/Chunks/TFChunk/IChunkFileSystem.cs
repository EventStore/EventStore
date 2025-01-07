// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk;

public interface IChunkFileSystem {
	ValueTask<IChunkHandle> OpenForReadAsync(string fileName, bool reduceFileCachePressure, CancellationToken token);

	ValueTask<ChunkHeader> ReadHeaderAsync(string fileName, CancellationToken token);

	ValueTask<ChunkFooter> ReadFooterAsync(string fileName, CancellationToken token);
}
