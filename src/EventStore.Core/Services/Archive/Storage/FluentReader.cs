// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Buffers;
using EventStore.Core.Services.Archive.Naming;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using FluentStorage.Blobs;
using Serilog;

namespace EventStore.Core.Services.Archive.Storage;

public abstract class FluentReader(IArchiveChunkNameResolver chunkNameResolver, string archiveCheckpointFile) {
	protected abstract ILogger Log { get; }
	protected abstract IBlobStorage BlobStorage { get; }

	public IArchiveChunkNameResolver ChunkNameResolver => chunkNameResolver;

	public async ValueTask<long> GetCheckpoint(CancellationToken ct) {
		await using var stream = await BlobStorage.OpenReadAsync(archiveCheckpointFile, ct);

		if (stream is null)
			return 0L;

		using var buffer = Memory.AllocateExactly<byte>(sizeof(long));
		await stream.ReadExactlyAsync(buffer.Memory, ct);
		var checkpoint = BinaryPrimitives.ReadInt64LittleEndian(buffer.Span);
		return checkpoint;
	}

	public async ValueTask<Stream> GetChunk(int logicalChunkNumber, CancellationToken ct) {
		var chunkFile = await chunkNameResolver.GetFileNameFor(logicalChunkNumber, ct);
		var stream = await BlobStorage.OpenReadAsync(chunkFile, ct);
		return stream ?? throw new ChunkDeletedException();
	}

	public abstract IAsyncEnumerable<string> ListChunks(CancellationToken ct);
}
