﻿// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading.Tasks;
using System.Threading;
using EventStore.Core.Services.Archive;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using Xunit;
using DotNext.Buffers;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Storage;

[Collection("ArchiveStorageTests")]
public class ArchiveStorageTests : ArchiveStorageTestsBase<ArchiveStorageTests> {
	[Theory]
	[StorageData.S3]
	[StorageData.FileSystem]
	public async Task can_read_chunk_entirely(StorageType storageType) {
		var sut = CreateSut(storageType);

		// create a chunk and upload it
		await using var chunk = await CreateLocalChunk(0, 0);
		await sut.StoreChunk(chunk, CancellationToken.None);

		// read the local chunk
		var localContent = await chunk.ReadAllBytes();

		// read the uploaded chunk
		using var buffer = Memory.AllocateExactly<byte>(1000);
		await sut.ReadAsync(0, buffer.Memory, offset: 0, CancellationToken.None);
		var chunkStreamContent = buffer.Memory.ToArray();

		// then
		Assert.Equal(localContent, chunkStreamContent);
	}

	[Theory]
	[StorageData.S3]
	[StorageData.FileSystem]
	public async Task can_read_chunk_partially(StorageType storageType) {
		var sut = CreateSut(storageType);

		// create a chunk and upload it
		await using var chunk = await CreateLocalChunk(0, 0);
		await sut.StoreChunk(chunk, CancellationToken.None);

		// read the local chunk
		var localContent = await chunk.ReadAllBytes();

		// read the uploaded chunk partially
		var start = localContent.Length / 2;
		var end = localContent.Length;
		var length = end - start;
		using var buffer = Memory.AllocateExactly<byte>(length);
		await sut.ReadAsync(0, buffer.Memory, offset: start, CancellationToken.None);
		var chunkStreamContent = buffer.Memory.ToArray();

		// then
		Assert.Equal(localContent[start..end], chunkStreamContent);
	}

	[Theory]
	[StorageData.S3]
	[StorageData.FileSystem]
	public async Task read_missing_chunk_throws_ChunkDeletedException(StorageType storageType) {
		var sut = CreateSut(storageType);

		await Assert.ThrowsAsync<ChunkDeletedException>(async () => {
			await sut.ReadAsync(33, Memory<byte>.Empty, offset: 0, CancellationToken.None);
		});
	}

	[Theory]
	[StorageData.S3]
	[StorageData.FileSystem]
	public async Task can_store_a_chunk(StorageType storageType) {
		var sut = CreateSut(storageType);
		await using var localChunk = await CreateLocalChunk(0, 0);
		Assert.True(await sut.StoreChunk(localChunk, CancellationToken.None));

		var localChunkContent = await localChunk.ReadAllBytes();

		using var buffer = Memory.AllocateExactly<byte>(1000);
		await sut.ReadAsync(0, buffer.Memory, offset: 0, CancellationToken.None);
		Assert.Equal(localChunkContent, buffer.Memory.ToArray());
	}

	[Theory]
	[StorageData.S3]
	[StorageData.FileSystem]
	public async Task can_write_and_read_checkpoint(StorageType storageType) {
		var checkpoint = Random.Shared.NextInt64();
		var sut = CreateSut(storageType);

		var writerSut = CreateSut(storageType);
		await writerSut.SetCheckpoint(checkpoint, CancellationToken.None);

		Assert.Equal(checkpoint, await sut.GetCheckpoint(CancellationToken.None));
	}
}
