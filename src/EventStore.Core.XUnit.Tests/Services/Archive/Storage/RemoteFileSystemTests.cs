// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive;
using EventStore.Core.Services.Archive.Storage.S3;
using EventStore.Core.Tests.TransactionLog;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Transforms.Identity;
using FluentStorage;
using FluentStorage.AWS.Blobs;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Storage;

[Collection("ArchiveStorageTests")]
public sealed class RemoteFileSystemTests : ArchiveStorageTestsBase<RemoteFileSystemTests> {
	[Theory]
	[StorageData.S3]
	//qq [StorageData.FileSystem]
	public async Task can_read_chunk_from_object_storage(StorageType storageType) {
		const int recordsCount = 10;

		// setup local chunk first
		var chunkName = Path.GetRandomFileName();
		var chunkLocalPath = Path.Combine(DbPath, chunkName);
		IReadOnlyList<ILogRecord> expectedRecords;
		using (var localChunk = await TFChunkHelper.CreateNewChunk(chunkLocalPath)) {
			expectedRecords = await GenerateRecords(recordsCount, localChunk);

			await localChunk.Complete(CancellationToken.None);
		}

		// upload the chunk
		Assert.True(await CreateWriterSut(storageType).StoreChunk(chunkLocalPath, 0, CancellationToken.None));
		chunkName = NameResolver.ResolveFileName(0);

		// download the chunk
		var fileSystem = new RemoteFileSystem();
		var actualRecords = new List<ILogRecord>(recordsCount);
		using (var remoteChunk = await TFChunk.FromCompletedFile(fileSystem, chunkName, verifyHash: false,
			       unbufferedRead: false, tracker: new TFChunkTracker.NoOp(),
			       getTransformFactory: static _ => new IdentityChunkTransformFactory())) {

			var logPosition = 0L;
			for (var i = 0; i < recordsCount; i++) {
				var result =
					await remoteChunk.TryReadClosestForward(remoteChunk.ChunkHeader.GetGlobalLogPosition(logPosition), CancellationToken.None);

				Assert.True(result.Success);
				actualRecords.Add(result.LogRecord);
				logPosition = result.NextPosition;
			}
		}

		Assert.Equal<ILogRecord>(expectedRecords, actualRecords);
	}

	private static async ValueTask<IReadOnlyList<ILogRecord>> GenerateRecords(int count, TFChunk chunk, CancellationToken token = default) {
		var records = new ILogRecord[count];

		var logPosition = 0L;
		for (var i = 0; i < count; i++) {
			var record = records[i] = LogRecord.Commit(
				chunk.ChunkHeader.GetGlobalLogPosition(logPosition),
				Guid.NewGuid(),
				i,
				i);
			var result = await chunk.TryAppend(record, token);
			Assert.True(result.Success);
			logPosition = result.NewPosition;
		}

		return records;
	}

	private sealed class RemoteFileSystem : IChunkFileSystem {
		private readonly IAwsS3BlobStorage _storage = (IAwsS3BlobStorage)StorageFactory.Blobs.AwsS3(
			awsCliProfileName: AwsCliProfileName,
			bucketName: AwsBucket,
			region: AwsRegion);

		public ValueTask<IChunkHandle> OpenForReadAsync(string fileName, IChunkFileSystem.ReadOptimizationHint hint,
			CancellationToken token)
			=> S3ChunkHandle.OpenForReadAsync(_storage.NativeBlobClient, _storage.BucketName, fileName, token);

		public IVersionedFileNamingStrategy NamingStrategy => throw new NotImplementedException();

		public IChunkFileSystem.IChunkEnumerable GetChunks() => throw new NotImplementedException();
	}
}
