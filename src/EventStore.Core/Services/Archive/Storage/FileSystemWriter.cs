﻿// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using Serilog;

namespace EventStore.Core.Services.Archive.Storage;

public class FileSystemWriter : IArchiveStorageWriter {
	protected static readonly ILogger Log = Serilog.Log.ForContext<FileSystemWriter>();

	private readonly string _archivePath;
	private readonly string _archiveCheckpointFile;
	private readonly Func<int?, int?, string> _getChunkPrefix;

	public FileSystemWriter(FileSystemOptions options, Func<int?, int?, string> getChunkPrefix, string archiveCheckpointFile) {
		_archivePath = options.Path;
		_getChunkPrefix = getChunkPrefix;
		_archiveCheckpointFile = archiveCheckpointFile;
	}

	public ValueTask<bool> SetCheckpoint(long checkpoint, CancellationToken ct) {
		try {
			var checkpointPath = Path.Combine(_archivePath, _archiveCheckpointFile);
			using var fs = File.OpenWrite(checkpointPath);

			Span<byte> buffer = stackalloc byte[8];
			BinaryPrimitives.WriteInt64LittleEndian(buffer, checkpoint);

			fs.Write(buffer);
			fs.Flush(flushToDisk: true);

			return ValueTask.FromResult(true);
		} catch (Exception ex) {
			Log.Error(ex, "Error while setting checkpoint to: 0x{checkpoint:X}", checkpoint);
			return ValueTask.FromResult(false);
		}
	}

	public async ValueTask<bool> StoreChunk(string chunkPath, string destinationFile, CancellationToken ct) {
		try {
			var destinationPath = Path.Combine(_archivePath, destinationFile);
			var tempPath = $"{destinationPath}.tmp";

			if (File.Exists(tempPath))
				File.Delete(tempPath);

			{
				await using var source = File.Open(
					path: chunkPath,
					options: new FileStreamOptions {
						Mode = FileMode.Open,
						Access = FileAccess.Read,
						Share = FileShare.Read,
						Options = FileOptions.SequentialScan | FileOptions.Asynchronous
					});

				await using var destination = File.Open(
					path: tempPath,
					options: new FileStreamOptions {
						Mode = FileMode.CreateNew,
						Access = FileAccess.ReadWrite,
						Share = FileShare.None,
						Options = FileOptions.Asynchronous,
						PreallocationSize = new FileInfo(chunkPath).Length
					});

				await source.CopyToAsync(destination, ct);
			}

			File.Move(tempPath, destinationPath, overwrite: true);

			return true;
		} catch (OperationCanceledException) {
			throw;
		} catch (Exception ex) {
			if (!File.Exists(chunkPath))
				throw new ChunkDeletedException();

			Log.Error(ex, "Error while storing chunk: {chunkFile}", Path.GetFileName(chunkPath));
			return false;
		}
	}
}
