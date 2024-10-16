// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archiver.Storage.Exceptions;

namespace EventStore.Core.Services.Archiver.Storage;

public interface IArchiveStorage {
	/// <summary>Stores a chunk in the archive</summary>
	/// <param name="chunkPath">The path of the chunk to archive</param>
	/// <param name="ct">The token to monitor for cancellation requests</param>
	/// <exception cref="ChunkDeletedException">Thrown if the chunk file is deleted while being archived</exception>
	/// <exception cref="OperationCanceledException">Thrown if cancellation was requested</exception>
	/// <returns>
	/// <see langword="true"/> if the chunk was successfully archived<br/>
	/// <see langword="false"/> if there was a recoverable error
	/// </returns>
	public ValueTask<bool> StoreChunk(string chunkPath, CancellationToken ct);

	/// <summary>Removes all chunks within the specified chunk number range from the archive, except the specified one</summary>
	/// <param name="chunkStartNumber">The chunk start number</param>
	/// <param name="chunkEndNumber">The chunk end number</param>
	/// <param name="exceptChunk">The file name of the chunk that must not be removed from the archive</param>
	/// <param name="ct">The token to monitor for cancellation requests</param>
	/// <exception cref="OperationCanceledException">Thrown if cancellation was requested</exception>
	/// <returns>
	/// <see langword="true"/> if the operation was successful<br/>
	/// <see langword="false"/> if there was a recoverable error
	/// </returns>
	public ValueTask<bool> RemoveChunks(int chunkStartNumber, int chunkEndNumber, string exceptChunk, CancellationToken ct);

	/// <summary>List all chunk files present in the archive</summary>
	/// <param name="ct">The token to monitor for cancellation requests</param>
	/// <returns>The file names of all chunks present in the archive</returns>
	public IAsyncEnumerable<string> ListChunks(CancellationToken ct);
}
