// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Archive.Archiver;
using EventStore.Core.Services.Archive.Storage;
using EventStore.Core.TransactionLog.Chunks;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Archiver;

public class ArchiverServiceTests {
	private static (ArchiverService, FakeArchiveStorage) CreateSut(
		TimeSpan? chunkStorageDelay = null,
		string[] existingChunks = null) {
		var archive = new FakeArchiveStorage(
			chunkStorageDelay ?? TimeSpan.Zero,
			existingChunks ?? Array.Empty<string>());
		var service = new ArchiverService(new FakeSubscriber(), archive);
		return (service, archive);
	}

	private static ChunkInfo GetChunkInfo(int chunkStartNumber, int chunkEndNumber, bool complete = true) {
		return new ChunkInfo {
			ChunkStartNumber = chunkStartNumber,
			ChunkEndNumber = chunkEndNumber,
			ChunkStartPosition = (long) chunkStartNumber * TFConsts.ChunkSize,
			ChunkEndPosition = (long) (chunkEndNumber + 1) * TFConsts.ChunkSize,
			IsCompleted = complete,
			ChunkFileName = $"{chunkStartNumber}-{chunkEndNumber}"
		};
	}

	private static async Task WaitFor(int numStores, FakeArchiveStorage archive) {
		var minDelay = TimeSpan.FromMilliseconds(200);
		await Task.Delay(minDelay);
		AssertEx.IsOrBecomesTrue(() => archive.Stores >= numStores, timeout: TimeSpan.FromSeconds(10));
	}

	[Fact]
	public async Task archives_a_completed_chunk_if_its_committed() {
		var (sut, archive) = CreateSut();

		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(0));

		var chunkInfo = GetChunkInfo(0, 0);
		sut.Handle(new SystemMessage.ChunkCompleted(chunkInfo));
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(chunkInfo.ChunkEndPosition));

		await WaitFor(numStores: 1, archive);

		Assert.Equal(["0-0"], archive.Chunks);
	}


	[Fact]
	public async Task doesnt_archive_a_completed_chunk_if_its_not_yet_committed() {
		var (sut, archive) = CreateSut();

		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(0));

		var chunkInfo = GetChunkInfo(0, 0);
		sut.Handle(new SystemMessage.ChunkCompleted(chunkInfo));
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(chunkInfo.ChunkEndPosition - 2));
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(chunkInfo.ChunkEndPosition - 1));

		await WaitFor(numStores: 0, archive);

		Assert.Equal([], archive.Chunks);
	}

	[Fact]
	public async Task archives_an_existing_chunk_if_its_complete() {
		var (sut, archive) = CreateSut();

		var chunkInfo = GetChunkInfo(0, 0, complete: true);
		sut.Handle(new SystemMessage.ChunkLoaded(chunkInfo));
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(chunkInfo.ChunkEndPosition));

		await WaitFor(numStores: 1, archive);

		Assert.Equal(["0-0"], archive.Chunks);
	}

	[Fact]
	public async Task doesnt_archive_an_existing_chunk_if_its_not_complete() {
		var (sut, archive) = CreateSut();

		var chunkInfo = GetChunkInfo(0, 0, complete: false);
		sut.Handle(new SystemMessage.ChunkLoaded(chunkInfo));
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(chunkInfo.ChunkEndPosition));

		await WaitFor(numStores: 0, archive);

		Assert.Equal([], archive.Chunks);
	}

	[Fact]
	public async Task doesnt_archive_an_existing_chunk_if_node_hasnt_joined_cluster() {
		var (sut, archive) = CreateSut();

		var chunkInfo = GetChunkInfo(0, 0, complete: true);
		sut.Handle(new SystemMessage.ChunkLoaded(chunkInfo));

		await WaitFor(numStores: 0, archive);

		Assert.Equal([], archive.Chunks);
	}

	[Fact]
	public async Task archives_a_switched_chunk() {
		var (sut, archive) = CreateSut();

		var chunkInfo = GetChunkInfo(0, 0, complete: true);
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(chunkInfo.ChunkEndPosition));
		sut.Handle(new SystemMessage.ChunkSwitched(chunkInfo));

		await WaitFor(numStores: 1, archive);

		Assert.Equal(["0-0"], archive.Chunks);
	}

	[Fact]
	public async Task archives_chunks_in_order() {
		var (sut, archive) = CreateSut(chunkStorageDelay: TimeSpan.FromMilliseconds(100));

		// chunks can be loaded out of order, since they are opened in parallel
		sut.Handle(new SystemMessage.ChunkLoaded(GetChunkInfo(2, 2, complete: true)));
		sut.Handle(new SystemMessage.ChunkLoaded(GetChunkInfo(1, 1, complete: true)));
		sut.Handle(new SystemMessage.ChunkLoaded(GetChunkInfo(0, 0, complete: true)));

		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(GetChunkInfo(4, 4, complete: true).ChunkEndPosition));

		sut.Handle(new SystemMessage.ChunkCompleted(GetChunkInfo(3, 3, complete: true)));
		sut.Handle(new SystemMessage.ChunkCompleted(GetChunkInfo(4, 4, complete: true)));

		await WaitFor(numStores: 5, archive);

		Assert.Equal(["0-0", "1-1", "2-2", "3-3", "4-4"], archive.Chunks);
	}

	[Fact]
	public async Task prioritizes_archiving_of_scavenged_chunks_over_new_chunks() {
		var (sut, archive) = CreateSut(chunkStorageDelay: TimeSpan.FromMilliseconds(100));

		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(0));

		sut.Handle(new SystemMessage.ChunkCompleted(GetChunkInfo(3, 3, complete: true)));
		sut.Handle(new SystemMessage.ChunkCompleted(GetChunkInfo(1, 2, complete: true)));
		sut.Handle(new SystemMessage.ChunkCompleted(GetChunkInfo(4, 4, complete: true)));

		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(GetChunkInfo(4, 4, complete: true).ChunkEndPosition));

		await WaitFor(numStores: 3, archive);

		Assert.Equal(["1-2", "3-3", "4-4"], archive.Chunks);
	}

	[Fact]
	public async Task doesnt_archive_existing_chunks_that_were_already_archived() {
		var (sut, archive) = CreateSut(existingChunks:	[ "0-0", "1-1" ]);

		sut.Handle(new SystemMessage.ChunkLoaded(GetChunkInfo(0, 0, complete: true)));
		sut.Handle(new SystemMessage.ChunkLoaded(GetChunkInfo(1, 1, complete: true)));
		sut.Handle(new SystemMessage.ChunkLoaded(GetChunkInfo(2, 2, complete: true)));
		sut.Handle(new SystemMessage.ChunkLoaded(GetChunkInfo(3, 3, complete: true)));
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(GetChunkInfo(3, 3, complete: true).ChunkEndPosition));

		await WaitFor(numStores: 2, archive);

		Assert.Equal([ "0-0", "1-1", "2-2", "3-3" ], archive.Chunks);
	}

	[Fact]
	public async Task removes_previous_chunks_with_same_chunk_numbers_from_the_archive() {
		var (sut, archive) = CreateSut(existingChunks:	[ "0-0", "1-1" ]);

		var chunkInfo = GetChunkInfo(0, 1, complete: true);
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(chunkInfo.ChunkEndPosition));
		sut.Handle(new SystemMessage.ChunkSwitched(chunkInfo));

		await WaitFor(numStores: 1, archive);

		Assert.Equal([ "0-1" ], archive.Chunks);
	}

	[Fact]
	public async Task cancels_archiving_when_system_shuts_down() {
		var (sut, archive) = CreateSut(TimeSpan.FromMilliseconds(100));

		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(0));

		var chunkInfo = GetChunkInfo(0, 0);
		sut.Handle(new SystemMessage.ChunkCompleted(chunkInfo));
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(chunkInfo.ChunkEndPosition));
		sut.Handle(new SystemMessage.BecomeShuttingDown(Guid.NewGuid(), exitProcess: true, shutdownHttp: true));

		await WaitFor(numStores: 0, archive);

		Assert.Equal([ ], archive.Chunks);
	}
}

internal class FakeSubscriber : ISubscriber {
	public void Subscribe<T>(IAsyncHandle<T> handler) where T : Message { }
	public void Unsubscribe<T>(IAsyncHandle<T> handler) where T : Message { }
}


internal class FakeArchiveStorage : IArchiveStorageWriter, IArchiveStorageReader, IArchiveStorageFactory {
	public List<string> Chunks;
	public int Stores => Interlocked.CompareExchange(ref _stores, 0, 0);
	private int _stores;

	private readonly TimeSpan _chunkStorageDelay;
	private readonly string[] _existingChunks;

	public FakeArchiveStorage(TimeSpan chunkStorageDelay, string[] existingChunks) {
		_chunkStorageDelay = chunkStorageDelay;
		_existingChunks = existingChunks;
		Chunks = new List<string>(existingChunks);
	}

	public IArchiveStorageReader CreateReader() => this;
	public IArchiveStorageWriter CreateWriter() => this;

	public async ValueTask<bool> StoreChunk(string chunkPath, CancellationToken ct) {
		await Task.Delay(_chunkStorageDelay, ct);
		Chunks.Add(chunkPath);
		Interlocked.Increment(ref _stores);
		return true;
	}

	public ValueTask<bool> RemoveChunks(int chunkStartNumber, int chunkEndNumber, string exceptChunk, CancellationToken ct) {
		var chunks = Chunks.ToArray();
		foreach (var chunk in chunks) {
			if (chunk == exceptChunk)
				continue;
			var tokens = chunk.Split("-");
			var curChunkStartNumber = int.Parse(tokens[0]);
			var curChunkEndNumber = int.Parse(tokens[0]);
			if (curChunkStartNumber >= chunkStartNumber && curChunkEndNumber <= chunkEndNumber)
				Chunks.Remove(chunk);
		}

		return ValueTask.FromResult(true);
	}

	public ValueTask<Stream> GetChunk(string chunkPath, CancellationToken ct) {
		throw new NotImplementedException();
	}

	public ValueTask<Stream> GetChunk(string chunkPath, long start, long end, CancellationToken ct) {
		throw new NotImplementedException();
	}

	public IAsyncEnumerable<string> ListChunks(CancellationToken ct) {
		return _existingChunks.ToAsyncEnumerable();
	}
}
