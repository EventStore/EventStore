// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk;

public class ArchiveBlobFileSystem : IBlobFileSystem {
	public ValueTask<IChunkHandle> OpenForReadAsync(string fileName, IBlobFileSystem.ReadOptimizationHint hint, CancellationToken token) {
		throw new NotImplementedException();
	}

	public ValueTask<ChunkFooter> ReadFooterAsync(string fileName, CancellationToken token) {
		throw new InvalidOperationException("Tried the read the footer of an archived chunk");
	}

	public ValueTask<ChunkHeader> ReadHeaderAsync(string fileName, CancellationToken token) {
		throw new InvalidOperationException("Tried the read the header of an archived chunk");
	}
}
