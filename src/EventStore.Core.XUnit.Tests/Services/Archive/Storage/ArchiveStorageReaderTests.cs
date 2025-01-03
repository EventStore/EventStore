// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using FluentStorage.Utils.Extensions;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Storage;

[Collection("ArchiveStorageTests")]
public abstract class ArchiveStorageReaderTests<T> : ArchiveStorageTestsBase<T> {
	[Fact]
	public async Task can_read_chunk_entirely() {
		var sut = CreateReaderSut(StorageType);

		// create a chunk and upload it
		var chunkPath = CreateLocalChunk(0, 0);
		var chunkFile = Path.GetFileName(chunkPath);
		await CreateWriterSut(StorageType).StoreChunk(chunkPath, chunkFile, CancellationToken.None);

		// read the local chunk
		var localContent = await File.ReadAllBytesAsync(chunkPath);

		// read the uploaded chunk
		using var chunkStream = await sut.GetChunk(chunkFile, CancellationToken.None);
		var chunkStreamContent = chunkStream.ToByteArray();

		// then
		Assert.Equal(localContent, chunkStreamContent);
	}

	[Fact]
	public async Task can_read_chunk_partially() {
		var sut = CreateReaderSut(StorageType);

		// create a chunk and upload it
		var chunkPath = CreateLocalChunk(0, 0);
		var chunkFile = Path.GetFileName(chunkPath);
		await CreateWriterSut(StorageType).StoreChunk(chunkPath, chunkFile, CancellationToken.None);

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

	[Fact]
	public async Task read_missing_chunk_throws_ChunkDeletedException() {
		var sut = CreateReaderSut(StorageType);

		await Assert.ThrowsAsync<ChunkDeletedException>(async () => {
			using var _ = await sut.GetChunk("missing-chunk", CancellationToken.None);
		});
	}

	[Fact]
	public async Task partial_read_missing_chunk_throws_ChunkDeletedException() {
		var sut = CreateReaderSut(StorageType);

		await Assert.ThrowsAsync<ChunkDeletedException>(async () => {
			using var _ = await sut.GetChunk("missing-chunk", 1, 2, CancellationToken.None);
		});
	}

	[Fact]
	public async Task can_list_chunks() {
		var sut = CreateReaderSut(StorageType);

		var chunk0 = CreateLocalChunk(0, 0);
		var chunk1 = CreateLocalChunk(1, 0);
		var chunk2 = CreateLocalChunk(2, 0);

		var writerSut = CreateWriterSut(StorageType);
		await writerSut.StoreChunk(chunk0, Path.GetFileName(chunk0), CancellationToken.None);
		await writerSut.StoreChunk(chunk1, Path.GetFileName(chunk1), CancellationToken.None);
		await writerSut.StoreChunk(chunk1, Path.GetFileName(chunk2), CancellationToken.None);

		var archivedChunks = sut.ListChunks(CancellationToken.None).ToEnumerable();
		Assert.Equal([
			Path.GetFileName(chunk0),
			Path.GetFileName(chunk1),
			Path.GetFileName(chunk2)
		], archivedChunks);
	}
}
