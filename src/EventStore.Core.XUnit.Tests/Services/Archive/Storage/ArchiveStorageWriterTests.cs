// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using FluentStorage.Utils.Extensions;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Storage;

[Collection("ArchiveStorageTests")]
public class ArchiveStorageWriterTests : ArchiveStorageTestsBase<ArchiveStorageWriterTests> {
	[Theory]
	[InlineData(StorageType.FileSystem)]
	[InlineData(StorageType.S3, Skip = SkipS3)]
	public async Task can_store_a_chunk(StorageType storageType) {
		var sut = CreateWriterSut(storageType);
		var localChunk = CreateLocalChunk(0, 0);
		Assert.True(await sut.StoreChunk(localChunk, CancellationToken.None));

		var localChunkContent = await File.ReadAllBytesAsync(localChunk);
		using var archivedChunkContent = await CreateReaderSut(storageType).GetChunk(Path.GetFileName(localChunk), CancellationToken.None);
		Assert.Equal(localChunkContent, archivedChunkContent.ToByteArray());
	}

	[Theory]
	[InlineData(StorageType.FileSystem)]
	[InlineData(StorageType.S3, Skip = SkipS3)]
	public async Task throws_chunk_deleted_exception_if_local_chunk_doesnt_exist(StorageType storageType) {
		var sut = CreateWriterSut(storageType);
		var localChunk = CreateLocalChunk(0, 0);
		File.Delete(localChunk);
		await Assert.ThrowsAsync<ChunkDeletedException>(async () => await sut.StoreChunk(localChunk, CancellationToken.None));
	}

	[Theory]
	[InlineData(StorageType.FileSystem)]
	public async Task can_remove_chunks(StorageType storageType) {
		var sut = CreateWriterSut(storageType);

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
