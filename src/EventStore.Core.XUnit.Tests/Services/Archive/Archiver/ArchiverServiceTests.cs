// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Archive.Archiver;
using EventStore.Core.Services.Archive.Storage;
using EventStore.Core.Tests;
using EventStore.Core.Tests.TransactionLog;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Archiver;

public sealed class ArchiverServiceTests : DirectoryPerTest<ArchiverServiceTests> {

	[Fact]
	public async Task chunk_order_preserved() {
		var indexDirectory = Fixture.GetFilePathFor("index");
		using var logFormat = LogFormatHelper<LogFormat.V2, string>.LogFormatFactory.Create(new() {
			IndexDirectory = indexDirectory,
		});
		var dbConfig = TFChunkHelper.CreateSizedDbConfig(Fixture.Directory, 0, chunkSize: 1024 * 1024);
		var dbCreator = await TFChunkDbCreationHelper<LogFormat.V2, string>.CreateAsync(dbConfig, logFormat);
		await using var result = await dbCreator
			.Chunk(Rec.Prepare(0, "test"), Rec.Commit(0, "test"))
			.Chunk(Rec.Prepare(1, "test"), Rec.Commit(1, "test"))
			.CompleteLastChunk()
			.CreateDb();

		var storage = new FakeArchiveStorage();
		await using (var archiver = new ArchiverService(new FakeSubscriber(), storage, result.Db.Manager)) {
			archiver.Handle(new ReplicationTrackingMessage.ReplicatedTo(long.MaxValue - 1L));

			var timeout = TimeSpan.FromSeconds(20);
			while (storage.NumStores < 2) {
				Assert.True(await storage.StoreChunkEvent.WaitAsync(timeout));
			}

		}

		Assert.True(storage.Checkpoint > 0L);
		Assert.Equal<int>([0, 1], storage.Chunks);
	}
}

file sealed class FakeSubscriber : ISubscriber {
	public void Subscribe<T>(IAsyncHandle<T> handler) where T : Message { }
	public void Unsubscribe<T>(IAsyncHandle<T> handler) where T : Message { }
}

file sealed class FakeArchiveStorage : IArchiveStorage {
	public readonly List<int> Chunks;
	public readonly AsyncAutoResetEvent StoreChunkEvent;
	public volatile int NumStores;

	public long Checkpoint;

	public FakeArchiveStorage(long existingCheckpoint = 0L) {
		Checkpoint = existingCheckpoint;
		Chunks = [];
		StoreChunkEvent = new(initialState: false);
	}

	public ValueTask<bool> StoreChunk(string chunkPath, int logicalChunkNumber, CancellationToken ct) {
		Chunks.Add(logicalChunkNumber);
		Interlocked.Increment(ref NumStores);
		StoreChunkEvent.Set();
		return ValueTask.FromResult(true);
	}

	public ValueTask<long> GetCheckpoint(CancellationToken ct) {
		return ValueTask.FromResult(Checkpoint);
	}

	public ValueTask<bool> SetCheckpoint(long checkpoint, CancellationToken ct) {
		Checkpoint = checkpoint;
		return ValueTask.FromResult(true);
	}

	public ValueTask<int> ReadAsync(int logicalChunkNumber, Memory<byte> buffer, long offset, CancellationToken ct) {
		throw new NotImplementedException();
	}

	public ValueTask<ArchivedChunkMetadata> GetMetadataAsync(int logicalChunkNumber, CancellationToken token) {
		throw new NotImplementedException();
	}
}
