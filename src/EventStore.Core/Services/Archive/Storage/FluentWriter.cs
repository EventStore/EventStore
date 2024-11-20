// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Threading;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using System.Threading.Tasks;
using Serilog;
using FluentStorage.Blobs;

namespace EventStore.Core.Services.Archive.Storage;

public class FluentWriter : IArchiveStorageWriter {
	protected static readonly ILogger Log = Serilog.Log.ForContext<FluentWriter>();
	readonly IBlobStorage _blobStorage;

	public FluentWriter(IBlobStorage blobStorage) {
		_blobStorage = blobStorage;
	}

	public async ValueTask<bool> StoreChunk(string chunkPath, CancellationToken ct) {
		var fileName = "unknown";
		try {
			fileName = Path.GetFileName(chunkPath);
			await _blobStorage.WriteFileAsync(fileName, filePath: chunkPath, ct);
			return true;
		} catch (FileNotFoundException) {
			throw new ChunkDeletedException();
		} catch (OperationCanceledException) {
			throw;
		} catch (Exception ex) {
			Log.Error(ex, "Error while storing chunk: {ChunkFile}", fileName);
			return false;
		}
	}

	public ValueTask<bool> RemoveChunks(
		int chunkStartNumber,
		int chunkEndNumber,
		string exceptChunk,
		CancellationToken ct) {

		throw new NotImplementedException();
	}
}
