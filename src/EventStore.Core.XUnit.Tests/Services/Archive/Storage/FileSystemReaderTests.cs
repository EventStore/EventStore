// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive;
using EventStore.Core.Services.Archive.Storage;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Storage;

public class FileSystemReaderTests : DirectoryPerTest<FileSystemReaderTests> {
	private const string ChunkPrefix = "chunk-";
	private string ArchivePath => Path.Combine(Fixture.Directory, "archive");
	private string DbPath => Path.Combine(Fixture.Directory, "db");

	public FileSystemReaderTests() {
		Directory.CreateDirectory(ArchivePath);
		Directory.CreateDirectory(DbPath);
	}

	private FileSystemReader CreateSut() {
		var namingStrategy = new VersionedPatternFileNamingStrategy(ArchivePath, ChunkPrefix);
		var reader = new FileSystemReader(
			new FileSystemOptions {
				Path = ArchivePath
			}, namingStrategy.GetPrefixFor);
		return reader;
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
}
