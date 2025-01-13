// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive.Naming;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using Serilog;

namespace EventStore.Core.Services.Archive.Storage;

public class ArchiveStorageWriter(
	IBlobStorage blobStorage,
	IArchiveChunkNameResolver chunkNameResolver,
	string archiveCheckpointFile)
	: IArchiveStorageWriter {

	private static readonly ILogger Log = Serilog.Log.ForContext<ArchiveStorageWriter>();

	public async ValueTask<bool> SetCheckpoint(long checkpoint, CancellationToken ct) {
		try {
			var buffer = new byte[sizeof(long)]; // todo: alas.
			BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(), checkpoint);
			await blobStorage.Store(buffer, archiveCheckpointFile, ct);
			return true;
		} catch (Exception ex) when (ex is not OperationCanceledException){
			Log.Error(ex, "Error while setting checkpoint to: {checkpoint} (0x{checkpoint:X})",
				checkpoint, checkpoint);
			return false;
		}
	}

	public async ValueTask<bool> StoreChunk(string sourceChunkPath, int logicalChunkNumber, CancellationToken ct) {
		try {
			var destinationFile = chunkNameResolver.ResolveFileName(logicalChunkNumber);
			await blobStorage.Store(sourceChunkPath, destinationFile, ct);
			return true;
		} catch (FileNotFoundException) {
			throw new ChunkDeletedException();
		} catch (Exception ex) when (ex is not OperationCanceledException) {
			if (!File.Exists(sourceChunkPath))
				throw new ChunkDeletedException();

			Log.Error(ex, "Error while storing chunk: {logicalChunkNumber} ({chunkPath})", logicalChunkNumber, sourceChunkPath);
			return false;
		}
	}
}
