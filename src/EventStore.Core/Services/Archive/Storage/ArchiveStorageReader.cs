// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Buffers;
using EventStore.Core.Services.Archive.Naming;
using EventStore.Core.Services.Archive.Storage.Exceptions;

namespace EventStore.Core.Services.Archive.Storage;

public class ArchiveStorageReader(
	IBlobStorage blobStorage,
	IArchiveChunkNameResolver chunkNameResolver,
	string archiveCheckpointFile)
	: IArchiveStorageReader {

	public IArchiveChunkNameResolver ChunkNameResolver => chunkNameResolver;

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
			//qq not sure that we want to convert this to ChunkDeletedException really? elsewhere in this file too
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
}
