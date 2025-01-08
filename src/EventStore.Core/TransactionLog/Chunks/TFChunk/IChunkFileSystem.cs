// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk;

public interface IChunkFileSystem {
	ValueTask<IChunkHandle> OpenForReadAsync(string fileName, bool reduceFileCachePressure, CancellationToken token);

	ValueTask<ChunkHeader> ReadHeaderAsync(string fileName, CancellationToken token);

	ValueTask<ChunkFooter> ReadFooterAsync(string fileName, CancellationToken token);

	IVersionedFileNamingStrategy NamingStrategy { get; }

	IChunkEnumerable GetChunks();

	public interface IChunkEnumerable : IAsyncEnumerable<TFChunkInfo> {

		// It is not a filter/limit, it is used to spot missing chunks
		int LastChunkNumber { get; set; }
	}
}
