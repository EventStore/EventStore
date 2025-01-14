// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Buffers;
using EventStore.Core.LogV2;
using EventStore.Core.Services.Archive;
using EventStore.Core.Tests.TransactionLog;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Transforms.Identity;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Storage;

[Collection("ArchiveStorageTests")]
public sealed class RemoteFileSystemTests : ArchiveStorageTestsBase<RemoteFileSystemTests> {
	// this is an integration test to check that the chunks are able to read through to remote storage
	[Theory]
	[StorageData.S3]
	[StorageData.FileSystem]
	public async Task chunk_can_read_record_from_object_storage(StorageType storageType) {
		const int recordsCount = 10;
		const int logicalChunkNumber = 42;

		var archive = CreateSut(storageType);

		// setup local chunk first
		var localChunkName = "my-chunk";
		var chunkLocalPath = Path.Combine(DbPath, localChunkName);
		IReadOnlyList<ILogRecord> expectedRecords;
		using (var localChunk = await TFChunkHelper.CreateNewChunk(chunkLocalPath)) {
			expectedRecords = await WriteRecords(recordsCount, localChunk);

			await localChunk.Complete(CancellationToken.None);
		}

		// upload the chunk
		Assert.True(await archive.StoreChunk(chunkLocalPath, logicalChunkNumber, CancellationToken.None));

		// read the remote chunk
		var codec = new PrefixingLocatorCodec();
		var remoteChunkName = codec.EncodeRemote(logicalChunkNumber);
		var fs = new FileSystemWithArchive(chunkSize: 4096, codec, new ChunkLocalFileSystem(DbPath), archive);
		var actualRecords = new List<ILogRecord>(recordsCount);
		using var remoteChunk = await TFChunk.FromCompletedFile(
			fs, remoteChunkName, verifyHash: false,
			unbufferedRead: false, tracker: new TFChunkTracker.NoOp(),
			getTransformFactory: static _ => new IdentityChunkTransformFactory());

		var logPosition = 0L;
		for (var i = 0; i < recordsCount; i++) {
			var result = await remoteChunk.TryReadClosestForward(logPosition, CancellationToken.None);
			Assert.True(result.Success);
			actualRecords.Add(result.LogRecord);
			logPosition = result.NextPosition;
		}

		Assert.Equal<ILogRecord>(expectedRecords, actualRecords);
	}

	private static async ValueTask<IReadOnlyList<ILogRecord>> WriteRecords(int count, TFChunk chunk, CancellationToken token = default) {
		var records = new ILogRecord[count];

		var recordFactory = new LogV2RecordFactory();
		var logPosition = 0L;
		for (var i = 0; i < count; i++) {
			var record = records[i] = recordFactory.CreatePrepare(
				logPosition: logPosition,
				correlationId: Guid.NewGuid(),
				eventId: Guid.NewGuid(),
				transactionPosition: logPosition,
				transactionOffset: 0,
				eventStreamId: "my-stream",
				expectedVersion: i,
				timeStamp: DateTime.Now,
				flags: PrepareFlags.SingleWrite,
				eventType: "my-event-type",
				data: Encoding.UTF8.GetBytes($"my-data-{i}"),
				metadata: Encoding.UTF8.GetBytes($"my-metadata-{i}"));
			var result = await chunk.TryAppend(record, token);
			Assert.True(result.Success);
			logPosition = result.NewPosition;
		}

		return records;
	}
}
