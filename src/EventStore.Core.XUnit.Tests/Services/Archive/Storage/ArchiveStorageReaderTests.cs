// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

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
	[StorageData.S3]
	[StorageData.FileSystem]
	public async Task can_read_chunk_entirely(StorageType storageType) {
		var sut = CreateReaderSut(storageType);

		// create a chunk and upload it
		var chunkPath = CreateLocalChunk(0, 0);
		await CreateWriterSut(storageType).StoreChunk(chunkPath, 0, CancellationToken.None);

		// read the local chunk
		var localContent = await File.ReadAllBytesAsync(chunkPath);

		// read the uploaded chunk
		await using var chunkStream = await sut.GetChunk(0, CancellationToken.None);
		var chunkStreamContent = chunkStream.ToByteArray();

		// then
		Assert.Equal(localContent, chunkStreamContent);
	}

	[Theory]
	[StorageData.S3]
	[StorageData.FileSystem]
	public async Task can_read_chunk_partially(StorageType storageType) {
		var sut = CreateReaderSut(storageType);

		// create a chunk and upload it
		var chunkPath = CreateLocalChunk(0, 0);
		await CreateWriterSut(storageType).StoreChunk(chunkPath, 0, CancellationToken.None);

		// read the local chunk
		var localContent = await File.ReadAllBytesAsync(chunkPath);

		// read the uploaded chunk partially
		var start = localContent.Length / 2;
		var end = localContent.Length;
		await using var chunkStream = await sut.GetChunk(0, start, end, CancellationToken.None);
		var chunkStreamContent = chunkStream.ToByteArray();

		// then
		Assert.Equal(localContent[start..end], chunkStreamContent);
	}

	[Theory]
	[StorageData.S3]
	[StorageData.FileSystem]
	public async Task read_missing_chunk_throws_ChunkDeletedException(StorageType storageType) {
		var sut = CreateReaderSut(storageType);

		await Assert.ThrowsAsync<ChunkDeletedException>(async () => {
			using var _ = await sut.GetChunk(33, CancellationToken.None);
		});
	}

	[Theory]
	[StorageData.S3]
	[StorageData.FileSystem]
	public async Task partial_read_missing_chunk_throws_ChunkDeletedException(StorageType storageType) {
		var sut = CreateReaderSut(storageType);

		await Assert.ThrowsAsync<ChunkDeletedException>(async () => {
			await using var _ = await sut.GetChunk(33, 1, 2, CancellationToken.None);
		});
	}
}
