// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using FluentStorage.Blobs;
using Serilog;

namespace EventStore.Core.Services.Archive.Storage;

public abstract class FluentReader(string archiveCheckpointFile) {
	protected abstract ILogger Log { get; }
	protected abstract IBlobStorage BlobStorage { get; }

	public async ValueTask<long> GetCheckpoint(CancellationToken ct) {
		await using var stream = await BlobStorage.OpenReadAsync(archiveCheckpointFile, ct);

		if (stream is null)
			return 0L;

		var buffer = ArrayPool<byte>.Shared.Rent(8);
		try {
			stream.ReadExactly(buffer.AsSpan(0, 8));
			var checkpoint = BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(0, 8));
			return checkpoint;
		} finally {
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	public async ValueTask<Stream> GetChunk(string chunkFile, CancellationToken ct) {
		var stream = await BlobStorage.OpenReadAsync(chunkFile, ct);
		return stream ?? throw new ChunkDeletedException();
	}

	public IAsyncEnumerable<string> ListChunks(CancellationToken ct) {
		throw new NotImplementedException();
	}
}
