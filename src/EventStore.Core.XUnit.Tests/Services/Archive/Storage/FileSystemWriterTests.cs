// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Storage;

public class FileSystemWriterTests : ArchiveStorageTestsBase<FileSystemWriterTests> {
	[Fact]
	public async Task can_store_a_chunk() {
		var sut = CreateWriterSut();
		var localChunk = CreateLocalChunk(0, 0);
		Assert.True(await sut.StoreChunk(localChunk, CancellationToken.None));

		var archivedChunk = Path.Combine(ArchivePath, Path.GetFileName(localChunk));

		Assert.True(File.Exists(archivedChunk));

		var localChunkContent = await File.ReadAllBytesAsync(localChunk);
		var archivedChunkContent = await File.ReadAllBytesAsync(archivedChunk);
		Assert.True(localChunkContent.AsSpan().SequenceEqual(archivedChunkContent));
	}

	[Fact]
	public async Task throws_chunk_deleted_exception_if_local_chunk_doesnt_exist() {
		var sut = CreateWriterSut();
		var localChunk = CreateLocalChunk(0, 0);
		File.Delete(localChunk);
		await Assert.ThrowsAsync<ChunkDeletedException>(async () => await sut.StoreChunk(localChunk, CancellationToken.None));
	}

	[Fact]
	public async Task can_remove_chunks() {
		var sut = CreateWriterSut();

		CreateArchiveChunk(0, 0);
		CreateArchiveChunk(1, 0);
		CreateArchiveChunk(2, 0);

		Assert.True(await sut.RemoveChunks(0, 2, "chunk-000001.000000", CancellationToken.None));

		var archivedChunks = Directory
			.EnumerateFiles(ArchivePath)
			.Select(Path.GetFileName);
		Assert.Equal(["chunk-000001.000000"], archivedChunks);
	}
}
