// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.Services.Archive.Storage;

public interface IArchiveStorageReader {
	/// <returns>A stream of the chunk's contents. Dispose this after use.</returns>
	public ValueTask<Stream> GetChunk(string chunkPath, CancellationToken ct);

	/// <returns>A stream of the chunk's contents over the specified range. Dispose this after use.</returns>
	public ValueTask<Stream> GetChunk(string chunkPath, long start, long end, CancellationToken ct);

	/// <summary>List all chunk files present in the archive</summary>
	/// <returns>The file names of all chunks present in the archive</returns>
	public IAsyncEnumerable<string> ListChunks(CancellationToken ct);
}
