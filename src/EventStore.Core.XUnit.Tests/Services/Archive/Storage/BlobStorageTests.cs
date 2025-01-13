// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Buffers;
using EventStore.Core.Services.Archive;
using EventStore.Core.Services.Archive.Storage;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Storage;

[Collection("ArchiveStorageTests")]
public class BlobStorageTests : DirectoryPerTest<BlobStorageTests> {
	protected const string AwsCliProfileName = "default";
	protected const string AwsRegion = "eu-west-1";
	protected const string AwsBucket = "archiver-unit-tests";

	protected const string ChunkPrefix = "chunk-";
	protected string ArchivePath => Path.Combine(Fixture.Directory, "archive");
	protected string LocalPath => Path.Combine(Fixture.Directory, "local");

	public BlobStorageTests() {
		Directory.CreateDirectory(ArchivePath);
		Directory.CreateDirectory(LocalPath);
	}

	IBlobStorage CreateSut(StorageType storageType) => storageType switch {
		StorageType.FileSystem =>
			new FileSystemBlobStorage(new() {
				Path = ArchivePath
			}),
		StorageType.S3 =>
			new S3BlobStorage(new() {
				AwsCliProfileName = AwsCliProfileName,
				Bucket = AwsBucket,
				Region = AwsRegion,
			}),
		_ => throw new NotImplementedException(),
	};

	string CreateFile(string fileName, int fileSize = 1000) {
		var path = Path.Combine(LocalPath, fileName);
		var content = new byte[fileSize];
		RandomNumberGenerator.Fill(content);
		File.WriteAllBytes(path, content);
		return path;
	}

	[Theory]
	[StorageData.S3]
	[StorageData.FileSystem]
	public async Task can_read_file_entirely(StorageType storageType) {
		var sut = CreateSut(storageType);

		// create a chunk and upload it
		var fileSize = 1000;
		var localPath = CreateFile("local.file", fileSize);
		await sut.Store(localPath, "output.file", CancellationToken.None);

		// read the local chunk
		var localContent = await File.ReadAllBytesAsync(localPath);

		// read the uploaded chunk
		using var buffer = Memory.AllocateExactly<byte>(fileSize);
		await sut.ReadAsync("output.file", buffer.Memory, offset: 0, CancellationToken.None);
		var chunkStreamContent = buffer.Memory.ToArray();

		// then
		Assert.Equal(localContent, chunkStreamContent);
	}

	[Theory]
	[StorageData.S3]
	[StorageData.FileSystem]
	public async Task can_store_and_read_file_partially(StorageType storageType) {
		var sut = CreateSut(storageType);

		// create a chunk and upload it
		var localPath = CreateFile("local.file");
		await sut.Store(localPath, "output.file", CancellationToken.None);

		// read the local chunk
		var localContent = await File.ReadAllBytesAsync(localPath);

		// read the uploaded chunk partially
		var start = localContent.Length / 2;
		var end = localContent.Length;
		var length = end - start;
		using var buffer = Memory.AllocateExactly<byte>(length);
		await sut.ReadAsync("output.file", buffer.Memory, offset: start, CancellationToken.None);
		var chunkStreamContent = buffer.Memory.ToArray();

		// then
		Assert.Equal(localContent[start..end], chunkStreamContent);
	}

	[Theory]
	[StorageData.S3]
	[StorageData.FileSystem]
	public async Task read_missing_chunk_throws_FileNotFoundException(StorageType storageType) {
		var sut = CreateSut(storageType);

		await Assert.ThrowsAsync<FileNotFoundException>(async () => {
			await sut.ReadAsync("missing-from-archive.file", Memory<byte>.Empty, offset: 0, CancellationToken.None);
		});
	}

	[Theory]
	[StorageData.S3]
	[StorageData.FileSystem]
	public async Task throws_file_not_found_exception_when_storing_non_existent_file(StorageType storageType) {
		var sut = CreateSut(storageType);
		await Assert.ThrowsAsync<FileNotFoundException>(async () =>
			await sut.Store("missing-input.file", "output.file", CancellationToken.None));
	}
}
