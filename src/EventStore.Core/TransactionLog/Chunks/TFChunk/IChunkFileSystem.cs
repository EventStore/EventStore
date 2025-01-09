// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk;

// This abstracts away the storage of chunks.
// Every type of the data tier needs to implement this interface. For instance, local file system and
// cloud-based object storage have their own implementations.
public interface IBlobFileSystem {
	ValueTask<IChunkHandle> OpenForReadAsync(string fileName, bool reduceFileCachePressure, CancellationToken token);

	ValueTask<ChunkHeader> ReadHeaderAsync(string fileName, CancellationToken token);

	ValueTask<ChunkFooter> ReadFooterAsync(string fileName, CancellationToken token);
}

// Chunks can be stored in different locations (say, archive vs local) but the access still goes through
// one implementation of this interface. That implementation can compose more than one implementation
// of IBlobFileSystem interface and act as a proxy
public interface IChunkFileSystem : IBlobFileSystem {
	IVersionedFileNamingStrategy NamingStrategy { get; }

	IChunkEnumerable GetChunks();

	public interface IChunkEnumerable : IAsyncEnumerable<TFChunkInfo> {
		// It is not a filter/limit, it is used to spot missing chunks
		int LastChunkNumber { get; set; }
	}
}
