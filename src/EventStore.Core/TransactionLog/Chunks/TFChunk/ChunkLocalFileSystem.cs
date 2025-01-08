// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Buffers;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk;

public sealed class ChunkLocalFileSystem(string path, string chunkFilePrefix = "chunk-") : IChunkFileSystem {
	private readonly VersionedPatternFileNamingStrategy _strategy = new(path, chunkFilePrefix);

	public IVersionedFileNamingStrategy NamingStrategy => _strategy;

	public ValueTask<IChunkHandle> OpenForReadAsync(string fileName, bool reduceFileCachePressure, CancellationToken token) {
		ValueTask<IChunkHandle> task;
		try {
			var options = new FileStreamOptions {
				Mode = FileMode.Open,
				Access = FileAccess.Read,
				Share = FileShare.ReadWrite,
				Options = reduceFileCachePressure
					? FileOptions.Asynchronous
					: FileOptions.RandomAccess | FileOptions.Asynchronous
			};

			task = new(new ChunkFileHandle(fileName, options));
		} catch (FileNotFoundException) {
			task = ValueTask.FromException<IChunkHandle>(
				new CorruptDatabaseException(new ChunkNotFoundException(fileName)));
		} catch (Exception e) {
			task = ValueTask.FromException<IChunkHandle>(e);
		}

		return task;
	}

	public async ValueTask<ChunkHeader> ReadHeaderAsync(string fileName, CancellationToken token) {
		using var handle = File.OpenHandle(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
			FileOptions.Asynchronous);

		var length = RandomAccess.GetLength(handle);
		if (length < ChunkFooter.Size + ChunkHeader.Size) {
			throw new CorruptDatabaseException(new BadChunkInDatabaseException(
				$"Chunk file '{fileName}' is bad. It does not have enough size for header and footer. File size is {length} bytes."));
		}

		using var buffer = Memory.AllocateExactly<byte>(ChunkHeader.Size);
		await RandomAccess.ReadAsync(handle, buffer.Memory, 0L, token);
		return new(buffer.Span);
	}

	public async ValueTask<ChunkFooter> ReadFooterAsync(string fileName, CancellationToken token) {
		using var handle = File.OpenHandle(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
			FileOptions.Asynchronous);

		var length = RandomAccess.GetLength(handle);
		if (length < ChunkFooter.Size + ChunkHeader.Size) {
			throw new CorruptDatabaseException(new BadChunkInDatabaseException(
				$"Chunk file '{fileName}' is bad. It does not have enough size for header and footer. File size is {length} bytes."));
		}

		using var buffer = Memory.AllocateExactly<byte>(ChunkFooter.Size);
		await RandomAccess.ReadAsync(handle, buffer.Memory, length - ChunkFooter.Size, token);
		return new(buffer.Span);
	}
}
