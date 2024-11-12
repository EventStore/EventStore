// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archiver;
using EventStore.Core.Services.Archiver.Storage;
using EventStore.Core.Services.Archiver.Storage.Exceptions;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services.Archiver.Storage;

public class FileSystemArchiveStorageTests : DirectoryPerTest<FileSystemArchiveStorageTests> {
	private const string ChunkPrefix = "chunk-";
	private string ArchivePath => Path.Combine(Fixture.Directory, "archive");
	private string DbPath => Path.Combine(Fixture.Directory, "db");

	public FileSystemArchiveStorageTests() {
		Directory.CreateDirectory(ArchivePath);
		Directory.CreateDirectory(DbPath);
	}

	private FileSystemArchiveStorage CreateSut() {
		var storage = new FileSystemArchiveStorage(
			new FileSystemOptions {
				Path = ArchivePath
			}, ChunkPrefix);
		return storage;
	}

	private static string CreateChunk(string path, int chunkStartNumber, int chunkVersion) {
		var namingStrategy = new VersionedPatternFileNamingStrategy(path, ChunkPrefix);

		var chunk = Path.Combine(path, namingStrategy.GetFilenameFor(chunkStartNumber, chunkVersion));
		var content = new byte[1000];
		RandomNumberGenerator.Fill(content);
		File.WriteAllBytes(chunk, content);
		return chunk;
	}

	private string CreateArchiveChunk(int chunkStartNumber, int chunkVersion) => CreateChunk(ArchivePath, chunkStartNumber, chunkVersion);
	private string CreateLocalChunk(int chunkStartNumber, int chunkVersion) => CreateChunk(DbPath, chunkStartNumber, chunkVersion);

	[Fact]
	public async Task can_store_a_chunk() {
		var sut = CreateSut();
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
		var sut = CreateSut();
		var localChunk = CreateLocalChunk(0, 0);
		File.Delete(localChunk);
		await Assert.ThrowsAsync<ChunkDeletedException>(async () => await sut.StoreChunk(localChunk, CancellationToken.None));
	}

	[Fact]
	public async Task can_list_chunks() {
		var sut = CreateSut();

		Assert.Equal(0, await sut.ListChunks(CancellationToken.None).CountAsync());

		var chunk0 = Path.GetFileName(CreateArchiveChunk(0, 0));
		var chunk1 = Path.GetFileName(CreateArchiveChunk(1, 0));
		var chunk2 = Path.GetFileName(CreateArchiveChunk(2, 0));

		var archivedChunks = sut.ListChunks(CancellationToken.None).ToEnumerable();
		Assert.Equal([chunk0, chunk1, chunk2], archivedChunks);
	}

	[Fact]
	public async Task can_remove_chunks() {
		var sut = CreateSut();

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
