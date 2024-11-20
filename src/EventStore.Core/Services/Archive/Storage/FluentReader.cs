// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using FluentStorage.Blobs;
using Serilog;

namespace EventStore.Core.Services.Archive.Storage;

public class FluentReader : IArchiveStorageReader {
	protected static readonly ILogger Log = Serilog.Log.ForContext<FluentReader>();
	readonly IBlobStorage _blobStorage;

	public FluentReader(IBlobStorage blobStorage) {
		_blobStorage = blobStorage;
	}

	public async ValueTask<Stream> GetChunk(string chunkPath, CancellationToken ct) {
		try {
			var fileName = Path.GetFileName(chunkPath);
			return await _blobStorage.OpenReadAsync(fileName, ct);
		} catch (FileNotFoundException) {
			throw new ChunkDeletedException();
		}
	}

	public IAsyncEnumerable<string> ListChunks(CancellationToken ct) {
		throw new NotImplementedException();
	}
}
