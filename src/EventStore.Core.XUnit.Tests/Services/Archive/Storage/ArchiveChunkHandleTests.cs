// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive;
using EventStore.Core.Services.Archive.Storage;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Storage;

[Collection("ArchiveStorageTests")]
public class ArchiveChunkHandleTests : ArchiveStorageTestsBase<ArchiveChunkHandleTests> {
	[Theory]
	[StorageData.S3]
	[StorageData.FileSystem]
	public async Task can_read_chunk_via_stream(StorageType storageType) {
		var archive = CreateSut(storageType);

		// create a chunk and upload it
		var chunkPath = CreateLocalChunk(0, 0);
		await archive.StoreChunk(chunkPath, 0, CancellationToken.None);

		// read the local chunk
		var localContent = await File.ReadAllBytesAsync(chunkPath);

		// read the uploaded chunk
		using var sut = await ArchivedChunkHandle.OpenForReadAsync(archive, 0, CancellationToken.None);
		using var inputStream = sut.CreateStream();
		var outputStream = new MemoryStream();
		await inputStream.CopyToAsync(outputStream, CancellationToken.None);

		// then
		Assert.Equal(localContent, outputStream.ToArray());
	}
}
