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
public class ArchiveStorageReaderTests : ArchiveStorageTestsBase<ArchiveStorageReaderTests> {
	[Theory]
	[InlineData(StorageType.FileSystem)]
	[InlineData(StorageType.S3, Skip = SkipS3)]
	public async Task can_read_chunk_entirely(StorageType storageType) {
		var sut = CreateReaderSut(storageType);

		// create a chunk and upload it
		var chunkPath = CreateLocalChunk(0, 0);
		var chunkFile = Path.GetFileName(chunkPath);
		await CreateWriterSut(storageType).StoreChunk(chunkPath, CancellationToken.None);

		// read the local chunk
		var localContent = await File.ReadAllBytesAsync(chunkPath);

		// read the uploaded chunk
		using var chunkStream = await sut.GetChunk(chunkFile, CancellationToken.None);
		var chunkStreamContent = chunkStream.ToByteArray();

		// then
		Assert.Equal(localContent, chunkStreamContent);
	}

	[Theory]
	[InlineData(StorageType.FileSystem)]
	[InlineData(StorageType.S3, Skip = SkipS3)]
	public async Task can_read_chunk_partially(StorageType storageType) {
		var sut = CreateReaderSut(storageType);

		// create a chunk and upload it
		var chunkPath = CreateLocalChunk(0, 0);
		var chunkFile = Path.GetFileName(chunkPath);
		await CreateWriterSut(storageType).StoreChunk(chunkPath, CancellationToken.None);

		// read the local chunk
		var localContent = await File.ReadAllBytesAsync(chunkPath);

		// read the uploaded chunk partially
		var start = localContent.Length / 2;
		var end = localContent.Length;
		using var chunkStream = await sut.GetChunk(chunkFile, start, end, CancellationToken.None);
		var chunkStreamContent = chunkStream.ToByteArray();

		// then
		Assert.Equal(localContent[start..end], chunkStreamContent);
	}

	[Theory]
	[InlineData(StorageType.FileSystem)]
	[InlineData(StorageType.S3, Skip = SkipS3)]
	public async Task read_missing_chunk_throws_ChunkDeletedException(StorageType storageType) {
		var sut = CreateReaderSut(storageType);

		await Assert.ThrowsAsync<ChunkDeletedException>(async () => {
			using var _ = await sut.GetChunk("missing-chunk", CancellationToken.None);
		});
	}

	[Theory]
	[InlineData(StorageType.FileSystem)]
	[InlineData(StorageType.S3, Skip = SkipS3)]
	public async Task partial_read_missing_chunk_throws_ChunkDeletedException(StorageType storageType) {
		var sut = CreateReaderSut(storageType);

		await Assert.ThrowsAsync<ChunkDeletedException>(async () => {
			using var _ = await sut.GetChunk("missing-chunk", 1, 2, CancellationToken.None);
		});
	}

	[Theory]
	[InlineData(StorageType.FileSystem)]
	public async Task can_list_chunks(StorageType storageType) {
		var sut = CreateReaderSut(storageType);

		Assert.Equal(0, await sut.ListChunks(CancellationToken.None).CountAsync());

		var chunk0 = Path.GetFileName(CreateArchiveChunk(0, 0));
		var chunk1 = Path.GetFileName(CreateArchiveChunk(1, 0));
		var chunk2 = Path.GetFileName(CreateArchiveChunk(2, 0));

		var archivedChunks = sut.ListChunks(CancellationToken.None).ToEnumerable();
		Assert.Equal([chunk0, chunk1, chunk2], archivedChunks);
	}
}
