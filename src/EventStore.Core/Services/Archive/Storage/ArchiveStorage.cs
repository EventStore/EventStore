// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Buffers;
using DotNext.IO;
using EventStore.Core.Services.Archive.Naming;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
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
			await blobStorage.ReadAsync(archiveCheckpointFile, buffer.Memory, offset: 0, ct);
			var checkpoint = BinaryPrimitives.ReadInt64LittleEndian(buffer.Span);
			return checkpoint;
		} catch (FileNotFoundException) {
			return 0;
		}
	}

	public async ValueTask<int> ReadAsync(int logicalChunkNumber, Memory<byte> buffer, long offset, CancellationToken ct) {
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
			return new(Size: metadata.Size);
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

	public async ValueTask<bool> StoreChunk(IChunkBlob chunk, CancellationToken ct) {
		var pipe = new Pipe();

		// process single chunk
		if (chunk.ChunkHeader.IsSingleLogicalChunk) {
			await StoreAsync(pipe, chunk, ct);
		} else {
			await foreach (var unmergedChunk in chunk.UnmergeAsync().WithCancellation(ct)) {
				// we need to dispose the unmerged chunk because it's temporary chunk
				using (unmergedChunk) {
					await StoreAsync(pipe, unmergedChunk, ct);
				}

				// reader/writer sides are completed at that point, it's safe to reset
				pipe.Reset();
			}
		}

		return true;
	}

	private Task StoreAsync(Pipe pipe, IChunkBlob chunk, CancellationToken ct) {
		Debug.Assert(chunk.ChunkHeader.IsSingleLogicalChunk);

		var copyingTask = WritePipeAsync(chunk, pipe.Writer, ct);
		var consumingTask = ReadPipeAsync(pipe.Reader, blobStorage,
			chunkNameResolver.ResolveFileName(chunk.ChunkHeader.ChunkStartNumber), ct);

		return Task.WhenAll(copyingTask, consumingTask);

		static async Task WritePipeAsync(IChunkBlob source, PipeWriter destination, CancellationToken token) {
			var destinationStream = destination.AsStream(leaveOpen: false);
			try {
				await source.CopyToAsync(destinationStream, token);
			} catch (Exception e) {
				await destination.CompleteAsync(e);
			} finally {
				// completed the write part of the pipe successfully, if not yet completed
				await destinationStream.DisposeAsync();
			}
		}

		static async Task ReadPipeAsync(PipeReader source, IBlobStorage destination, string destinationFile,
			CancellationToken token) {
			var sourceStream = source.AsStream(leaveOpen: false);
			try {
				await destination.StoreAsync(sourceStream, destinationFile, token);
			} catch (Exception e) {
				await source.CompleteAsync(e);
			} finally {
				// completed the read part of the pipe successfully, if not yet completed
				await sourceStream.DisposeAsync();
			}
		}
	}
}
