// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using FluentStorage.Utils.Extensions;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Storage;

[Collection("ArchiveStorageTests")]
public abstract class ArchiveStorageWriterTests : ArchiveStorageTestsBase<ArchiveStorageWriterTests> {
	[RemoteStorageTheory]
	[InlineData(StorageType.S3)]
	public async Task can_store_a_chunk(StorageType storageType) {
		var sut = CreateWriterSut(storageType);
		var localChunk = CreateLocalChunk(0, 0);
		Assert.True(await sut.StoreChunk(localChunk, 0, CancellationToken.None));

		var localChunkContent = await File.ReadAllBytesAsync(localChunk);
		await using var archivedChunkContent = await CreateReaderSut(storageType).GetChunk(0, CancellationToken.None);
		Assert.Equal(localChunkContent, archivedChunkContent.ToByteArray());
	}

	[RemoteStorageTheory]
	[InlineData(StorageType.S3)]
	public async Task throws_chunk_deleted_exception_if_local_chunk_doesnt_exist(StorageType storageType) {
		var sut = CreateWriterSut(storageType);
		var localChunk = CreateLocalChunk(0, 0);
		File.Delete(localChunk);
		await Assert.ThrowsAsync<ChunkDeletedException>(async () => await sut.StoreChunk(localChunk, 0, CancellationToken.None));
	}

	[RemoteStorageTheory]
	[InlineData(StorageType.S3)]
	public async Task can_write_and_read_checkpoint(StorageType storageType) {
		var checkpoint = Random.Shared.NextInt64();
		var sut = CreateReaderSut(storageType);

		var writerSut = CreateWriterSut(storageType);
		await writerSut.SetCheckpoint(checkpoint, CancellationToken.None);

		Assert.Equal(checkpoint, await sut.GetCheckpoint(CancellationToken.None));
	}
}
