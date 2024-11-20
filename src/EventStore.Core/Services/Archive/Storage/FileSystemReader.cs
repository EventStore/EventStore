// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using Serilog;

namespace EventStore.Core.Services.Archive.Storage;

public class FileSystemReader : IArchiveStorageReader {
	protected static readonly ILogger Log = Serilog.Log.ForContext<FileSystemReader>();

	private readonly string _archivePath;
	private readonly Func<int?, int?, string> _getChunkPrefix;

	public FileSystemReader(FileSystemOptions options, Func<int?, int?, string> getChunkPrefix) {
		_archivePath = options.Path;
		_getChunkPrefix = getChunkPrefix;
	}

	public async ValueTask<Stream> GetChunk(string chunkPath, CancellationToken ct) {
		try {
			return File.OpenRead(chunkPath);
		} catch (FileNotFoundException) {
			throw new ChunkDeletedException();
		}
	}

	public async ValueTask<Stream> GetChunk(string chunkPath, long start, long end, CancellationToken ct) {
		var longLength = end - start;

		if (longLength > int.MaxValue)
			throw new InvalidOperationException($"Attempted to read too much from chunk {chunkPath}. Start: {start}. End {end}");
		else if (longLength < 0)
			throw new InvalidOperationException($"Attempted to read negative amount from chunk {chunkPath}. Start: {start}. End {end}");

		var length = (int)longLength;

		try {
			using var fileStream = File.OpenRead(chunkPath);
			fileStream.Position = start;

			var target = MemoryOwner<byte>.Allocate(length, AllocationMode.Default).AsStream();
			await fileStream.CopyToAsync(target, ct);
			target.Position = 0;
			return target;

		} catch (FileNotFoundException) {
			throw new ChunkDeletedException();
		}
	}

	public IAsyncEnumerable<string> ListChunks(CancellationToken ct) {
		return new DirectoryInfo(_archivePath)
			.EnumerateFiles($"{_getChunkPrefix(null, null)}*")
			.Select(chunk => chunk.Name)
			.Order()
			.ToAsyncEnumerable();
	}
}
