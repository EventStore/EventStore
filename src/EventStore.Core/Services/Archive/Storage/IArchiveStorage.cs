// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;

namespace EventStore.Core.Services.Archive.Storage;

public interface IArchiveStorage : IArchiveStorageReader {
	/// <summary>Sets the position in the transaction log up to which all chunks have been archived</summary>
	public ValueTask SetCheckpoint(long checkpoint, CancellationToken ct);

	/// <summary>Stores a chunk in the archive</summary>
	public ValueTask StoreChunk(IChunkBlob chunk, CancellationToken ct);
}
