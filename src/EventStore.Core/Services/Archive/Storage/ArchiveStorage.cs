// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Buffers;
using DotNext.IO;
using EventStore.Core.Services.Archive.Naming;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using Microsoft.Win32.SafeHandles;
using Serilog;

namespace EventStore.Core.Services.Archive.Storage;

public class ArchiveStorage(
	IBlobStorage blobStorage,
	IArchiveChunkNameResolver chunkNameResolver,
	string archiveCheckpointFile)
	: IArchiveStorage {

	private static readonly ILogger Log = Serilog.Log.ForContext<ArchiveStorage>();

	public async ValueTask<long> GetCheckpoint(CancellationToken ct) {
		try {
			using var buffer = Memory.AllocateExactly<byte>(sizeof(long));
			await blobStorage.ReadAsync(archiveCheckpointFile, buffer.Memory, offset: 0, ct); //qq version check
			var checkpoint = BinaryPrimitives.ReadInt64LittleEndian(buffer.Span);
			return checkpoint;
		} catch (FileNotFoundException) {
			return 0;
		}
	}

	public async ValueTask<(int, string)> ReadAsync(int logicalChunkNumber, Memory<byte> buffer, long offset, CancellationToken ct) {
		try {
			var chunkFile = chunkNameResolver.ResolveFileName(logicalChunkNumber);
			return await blobStorage.ReadAsync(chunkFile, buffer, offset, ct);
		} catch (FileNotFoundException) {
			throw new ChunkDeletedException();
		}
	}

	public async ValueTask<ArchivedChunkMetadata> GetMetadataAsync(int logicalChunkNumber, CancellationToken ct) {
		try {
			var objectName = chunkNameResolver.ResolveFileName(logicalChunkNumber);
			var metadata = await blobStorage.GetMetadataAsync(objectName, ct);
			return new(
				PhysicalSize: metadata.Size,
				ETag: metadata.ETag);
		} catch (FileNotFoundException) {
			throw new ChunkDeletedException();
		}
	}

	public async ValueTask<bool> SetCheckpoint(long checkpoint, CancellationToken ct) {
		var buffer = Memory.AllocateExactly<byte>(sizeof(long));
		var stream = StreamSource.AsStream(buffer.Memory);
		try {
			BinaryPrimitives.WriteInt64LittleEndian(buffer.Span, checkpoint);
			await blobStorage.StoreAsync(stream, archiveCheckpointFile, ct);
			return true;
		} catch (Exception ex) when (ex is not OperationCanceledException) {
			Log.Error(ex, "Error while setting checkpoint to: {checkpoint} (0x{checkpoint:X})",
				checkpoint, checkpoint);
			return false;
		} finally {
			await stream.DisposeAsync();
			buffer.Dispose();
		}
	}

	public async ValueTask<bool> StoreChunk(string sourceChunkPath, int logicalChunkNumber, CancellationToken ct) {
		var handle = default(SafeFileHandle);
		var stream = default(Stream);
		try {
			handle = File.OpenHandle(sourceChunkPath, options: FileOptions.Asynchronous | FileOptions.SequentialScan);
			stream = handle.AsUnbufferedStream(FileAccess.Read);
			var destinationFile = chunkNameResolver.ResolveFileName(logicalChunkNumber);
			await blobStorage.StoreAsync(stream, destinationFile, ct);
			return true;
		} catch (FileNotFoundException) {
			throw new ChunkDeletedException();
		} catch (Exception ex) when (ex is not OperationCanceledException) {
			if (!File.Exists(sourceChunkPath))
				throw new ChunkDeletedException();

			Log.Error(ex, "Error while storing chunk: {logicalChunkNumber} ({chunkPath})", logicalChunkNumber,
				sourceChunkPath);
			return false;
		} finally {
			await (stream?.DisposeAsync() ?? ValueTask.CompletedTask);
			handle?.Dispose();
		}
	}
}
