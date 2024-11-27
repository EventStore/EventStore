// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive.Storage.Exceptions;

namespace EventStore.Core.Services.Archive.Storage;

public interface IArchiveStorageWriter {
	/// <summary>Sets the position in the transaction log up to which all chunks have been archived</summary>
	/// <returns>
	/// <see langword="true"/> if the checkpoint was set<br/>
	/// <see langword="false"/> if the operation failed and the caller needs to retry after some time
	/// </returns>
	public ValueTask<bool> SetCheckpoint(long checkpoint, CancellationToken ct);

	/// <summary>Stores a chunk in the archive</summary>
	/// <param name="chunkPath">The path of the chunk to archive</param>
	/// <exception cref="ChunkDeletedException">Thrown if the chunk file is deleted while being archived</exception>
	/// <returns>
	/// <see langword="true"/> if the chunk was successfully archived<br/>
	/// <see langword="false"/> if the operation failed and the caller needs to retry after some time
	/// </returns>
	public ValueTask<bool> StoreChunk(string chunkPath, CancellationToken ct);

	/// <summary>Removes all chunks within the specified chunk number range from the archive, except the specified one</summary>
	/// <param name="chunkStartNumber">The chunk start number</param>
	/// <param name="chunkEndNumber">The chunk end number</param>
	/// <param name="exceptChunk">The file name of the chunk that must not be removed from the archive</param>
	/// <returns>
	/// <see langword="true"/> if the operation was successful<br/>
	/// <see langword="false"/> if the operation failed and the caller needs to retry after some time
	/// </returns>
	public ValueTask<bool> RemoveChunks(int chunkStartNumber, int chunkEndNumber, string exceptChunk, CancellationToken ct);
}
