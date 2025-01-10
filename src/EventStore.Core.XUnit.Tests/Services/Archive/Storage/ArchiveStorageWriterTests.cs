// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using FluentStorage.Utils.Extensions;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Storage;

[Collection("ArchiveStorageTests")]
public abstract class ArchiveStorageWriterTests<T> : ArchiveStorageTestsBase<T> {
	[Fact]
	public async Task can_store_a_chunk() {
		var sut = CreateWriterSut(StorageType);
		var localChunk = CreateLocalChunk(0, 0);
		Assert.True(await sut.StoreChunk(localChunk, 0, CancellationToken.None));

		var localChunkContent = await File.ReadAllBytesAsync(localChunk);
		await using var archivedChunkContent = await CreateReaderSut(StorageType).GetChunk(0, CancellationToken.None);
		Assert.Equal(localChunkContent, archivedChunkContent.ToByteArray());
	}

	[Fact]
	public async Task throws_chunk_deleted_exception_if_local_chunk_doesnt_exist() {
		var sut = CreateWriterSut(StorageType);
		var localChunk = CreateLocalChunk(0, 0);
		File.Delete(localChunk);
		await Assert.ThrowsAsync<ChunkDeletedException>(async () => await sut.StoreChunk(localChunk, 0, CancellationToken.None));
	}

	[Fact]
	public async Task can_write_and_read_checkpoint() {
		var checkpoint = Random.Shared.NextInt64();
		var sut = CreateReaderSut(StorageType);

		var writerSut = CreateWriterSut(StorageType);
		await writerSut.SetCheckpoint(checkpoint, CancellationToken.None);

		Assert.Equal(checkpoint, await sut.GetCheckpoint(CancellationToken.None));
	}
}
