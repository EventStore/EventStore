// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Services.Archive.Storage;
using EventStore.Core.Services.Archive;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using System.IO;
using System.Security.Cryptography;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Storage;

public class ArchiveStorageTestsBase<T> : DirectoryPerTest<T> {
	protected const string ChunkPrefix = "chunk-";
	protected string ArchivePath => Path.Combine(Fixture.Directory, "archive");
	protected string DbPath => Path.Combine(Fixture.Directory, "db");

	public ArchiveStorageTestsBase() {
		Directory.CreateDirectory(ArchivePath);
		Directory.CreateDirectory(DbPath);
	}

	protected FileSystemWriter CreateWriterSut() {
		var namingStrategy = new VersionedPatternFileNamingStrategy(ArchivePath, ChunkPrefix);
		var sut = new FileSystemWriter(
			new FileSystemOptions {
				Path = ArchivePath
			}, namingStrategy.GetPrefixFor);
		return sut;
	}

	protected FileSystemReader CreateReaderSut() {
		var namingStrategy = new VersionedPatternFileNamingStrategy(ArchivePath, ChunkPrefix);
		var sut = new FileSystemReader(
			new FileSystemOptions {
				Path = ArchivePath
			}, namingStrategy.GetPrefixFor);
		return sut;
	}

	protected static string CreateChunk(string path, int chunkStartNumber, int chunkVersion) {
		var namingStrategy = new VersionedPatternFileNamingStrategy(path, ChunkPrefix);

		var chunk = Path.Combine(path, namingStrategy.GetFilenameFor(chunkStartNumber, chunkVersion));
		var content = new byte[1000];
		RandomNumberGenerator.Fill(content);
		File.WriteAllBytes(chunk, content);
		return chunk;
	}

	protected string CreateArchiveChunk(int chunkStartNumber, int chunkVersion) => CreateChunk(ArchivePath, chunkStartNumber, chunkVersion);
	protected string CreateLocalChunk(int chunkStartNumber, int chunkVersion) => CreateChunk(DbPath, chunkStartNumber, chunkVersion);
}
