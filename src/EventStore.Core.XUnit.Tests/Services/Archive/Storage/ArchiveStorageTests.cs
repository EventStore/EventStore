// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
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
		var chunkPath = CreateLocalChunk(0, 0);
		await sut.StoreChunk(chunkPath, 0, CancellationToken.None);

		// read the local chunk
		var localContent = await File.ReadAllBytesAsync(chunkPath);

		// read the uploaded chunk
		using var buffer = Memory.AllocateExactly<byte>(1048);
		var read = await sut.ReadAsync(0, buffer.Memory, offset: 0, CancellationToken.None);

		// then
		Assert.Equal(localContent, buffer.Memory[..read].ToArray());
	}

	[Theory]
	[StorageData.S3]
	[StorageData.FileSystem]
	public async Task can_read_chunk_partially(StorageType storageType) {
		var sut = CreateSut(storageType);

		// create a chunk and upload it
		var chunkPath = CreateLocalChunk(0, 0);
		await sut.StoreChunk(chunkPath, 0, CancellationToken.None);

		// read the local chunk
		var localContent = await File.ReadAllBytesAsync(chunkPath);

		// read the uploaded chunk partially
		var start = localContent.Length / 2;
		var end = localContent.Length;
		var length = end - start;
		using var buffer = Memory.AllocateExactly<byte>(1048);
		var read = await sut.ReadAsync(0, buffer.Memory, offset: start, CancellationToken.None);

		// then
		Assert.Equal(localContent[start..end], buffer.Memory[..read].ToArray());
	}

	[Theory]
	[StorageData.S3]
	[StorageData.FileSystem]
	public async Task can_read_outside_of_range(StorageType storageType) {
		var sut = CreateSut(storageType);

		// create a chunk and upload it
		var chunkPath = CreateLocalChunk(0, 0);
		await sut.StoreChunk(chunkPath, 0, CancellationToken.None);

		// read the uploaded chunk partially
		using var buffer = Memory.AllocateExactly<byte>(1048);
		Assert.Equal(0, await sut.ReadAsync(0, buffer.Memory, offset: 1_000, CancellationToken.None));
		Assert.Equal(0, await sut.ReadAsync(0, buffer.Memory, offset: 1_000_000, CancellationToken.None));
	}

	// not useful in practice but to ensure consistency across the implementations
	[Theory]
	[StorageData.S3]
	[StorageData.FileSystem]
	public async Task can_read_with_zero_length_buffer(StorageType storageType) {
		var sut = CreateSut(storageType);

		// create a chunk and upload it
		var chunkPath = CreateLocalChunk(0, 0);
		await sut.StoreChunk(chunkPath, 0, CancellationToken.None);

		// read the uploaded chunk partially
		using var buffer = Memory.AllocateExactly<byte>(0);
		Assert.Equal(0, await sut.ReadAsync(0, buffer.Memory, offset: 0, CancellationToken.None));
	}

	[Theory]
	[StorageData.S3]
	[StorageData.FileSystem]
	public async Task read_with_negative_offset_throws(StorageType storageType) {
		var sut = CreateSut(storageType);

		// create a chunk and upload it
		var chunkPath = CreateLocalChunk(0, 0);
		await sut.StoreChunk(chunkPath, 0, CancellationToken.None);

		// read the uploaded chunk partially
		using var buffer = Memory.AllocateExactly<byte>(0);
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => {
			await sut.ReadAsync(0, buffer.Memory, offset: -1, CancellationToken.None);
		});
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
		var localChunk = CreateLocalChunk(0, 0);
		Assert.True(await sut.StoreChunk(localChunk, 0, CancellationToken.None));

		var localChunkContent = await File.ReadAllBytesAsync(localChunk);

		using var buffer = Memory.AllocateExactly<byte>(1048);
		var read = await sut.ReadAsync(0, buffer.Memory, offset: 0, CancellationToken.None);
		Assert.Equal(localChunkContent, buffer.Memory[..read].ToArray());
	}

	[Theory]
	[StorageData.S3]
	[StorageData.FileSystem]
	public async Task throws_chunk_deleted_exception_if_local_chunk_doesnt_exist(StorageType storageType) {
		var sut = CreateSut(storageType);
		var localChunk = CreateLocalChunk(0, 0);
		File.Delete(localChunk);
		await Assert.ThrowsAsync<ChunkDeletedException>(async () => await sut.StoreChunk(localChunk, 0, CancellationToken.None));
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
