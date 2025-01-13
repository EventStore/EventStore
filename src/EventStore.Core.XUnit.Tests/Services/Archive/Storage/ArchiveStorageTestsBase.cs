// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.IO;
using System.Security.Cryptography;
using EventStore.Core.Services.Archive;
using EventStore.Core.Services.Archive.Naming;
using EventStore.Core.Services.Archive.Storage;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Storage;

public abstract class ArchiveStorageTestsBase<T> : DirectoryPerTest<T> {
	protected const string AwsCliProfileName = "default";
	protected const string AwsRegion = "eu-west-1";
	protected const string AwsBucket = "archiver-unit-tests";

	protected const string ChunkPrefix = "chunk-";
	protected string ArchivePath => Path.Combine(Fixture.Directory, "archive");
	protected string DbPath => Path.Combine(Fixture.Directory, "db");

	public ArchiveStorageTestsBase() {
		Directory.CreateDirectory(ArchivePath);
		Directory.CreateDirectory(DbPath);
	}

	protected IArchiveStorage CreateSut(StorageType storageType) {
		var namingStrategy = new VersionedPatternFileNamingStrategy(ArchivePath, ChunkPrefix);
		var nameResolver  = new ArchiveChunkNameResolver(namingStrategy);
		var archiveStorage = ArchiveStorageFactory.Create(
				new() {
					StorageType = storageType,
					FileSystem = new() {
						Path = ArchivePath
					},
					S3 = new() {
						AwsCliProfileName = AwsCliProfileName,
						Bucket = AwsBucket,
						Region = AwsRegion,
					}
				},
				nameResolver);
		return archiveStorage;
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
