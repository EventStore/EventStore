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
using EventStore.Core.Services.Archive.Archiver.Unmerger;
using EventStore.Core.Services.Archive.Naming;
using EventStore.Core.TransactionLog.Chunks;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Archiver;

public class ArchiverServiceTests {
	private const int ChunkSize = TFConsts.ChunkSize;

	private static (ArchiverService, FakeArchiveStorage) CreateSut(
		TimeSpan? chunkStorageDelay = null,
		string[] existingChunks = null,
		long? existingCheckpoint = null) {
		var archive = new FakeArchiveStorage(
			chunkStorageDelay ?? TimeSpan.Zero,
			existingChunks ?? Array.Empty<string>(),
			existingCheckpoint ?? 0L);
		var service = new ArchiverService(new FakeSubscriber(), archive, new FakeUnmerger(), new FakeArchiveChunkNamer());
		return (service, archive);
	}

	private static ChunkInfo GetChunkInfo(int chunkStartNumber, int chunkEndNumber, bool complete = true) {
		return new ChunkInfo {
			ChunkStartNumber = chunkStartNumber,
			ChunkEndNumber = chunkEndNumber,
			ChunkStartPosition = (long) chunkStartNumber * ChunkSize,
			ChunkEndPosition = (long) (chunkEndNumber + 1) * ChunkSize,
			IsCompleted = complete,
			ChunkLocator = $"{chunkStartNumber}-{chunkEndNumber}"
		};
	}

	private static async Task WaitFor(FakeArchiveStorage archive, int numStores = -1, int numCheckpoints = -1) {
		var minDelay = TimeSpan.FromMilliseconds(200);
		await Task.Delay(minDelay);
		if (numStores >= 0)
			AssertEx.IsOrBecomesTrue(() => archive.NumStores >= numStores, timeout: TimeSpan.FromSeconds(10));

		if (numCheckpoints >= 0)
			AssertEx.IsOrBecomesTrue(() => archive.NumCheckpoints >= numCheckpoints, timeout: TimeSpan.FromSeconds(10));
	}

	[Fact]
	public async Task archives_a_completed_chunk_if_its_committed() {
		var (sut, archive) = CreateSut();

		var chunkInfo = GetChunkInfo(0, 0);
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(0));
		sut.Handle(new SystemMessage.ChunkCompleted(chunkInfo));
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(chunkInfo.ChunkEndPosition));

		await WaitFor(archive, numStores: 1);

		Assert.Equal(["0-0.renamed"], archive.Chunks);
	}

	[Fact]
	public async Task doesnt_archive_a_completed_chunk_if_its_not_yet_committed() {
		var (sut, archive) = CreateSut();

		var chunkInfo = GetChunkInfo(0, 0);
		sut.Handle(new SystemMessage.ChunkCompleted(chunkInfo));
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(0));
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(chunkInfo.ChunkEndPosition - 2));
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(chunkInfo.ChunkEndPosition - 1));

		await WaitFor(archive, numStores: 0);

		Assert.Equal([], archive.Chunks);
	}

	[Fact]
	public async Task archives_an_existing_chunk_if_its_complete() {
		var (sut, archive) = CreateSut();

		var chunkInfo = GetChunkInfo(0, 0, complete: true);
		sut.Handle(new SystemMessage.ChunkLoaded(chunkInfo));
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(0));

		await WaitFor(archive, numStores: 1);

		Assert.Equal(["0-0.renamed"], archive.Chunks);
	}

	[Fact]
	public async Task doesnt_archive_an_existing_chunk_if_its_not_complete() {
		var (sut, archive) = CreateSut();

		var chunkInfo = GetChunkInfo(0, 0, complete: false);
		sut.Handle(new SystemMessage.ChunkLoaded(chunkInfo));
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(0));

		await WaitFor(archive, numStores: 0);

		Assert.Equal([], archive.Chunks);
	}

	[Fact]
	public async Task doesnt_archive_an_existing_chunk_if_the_node_hasnt_joined_the_cluster_yet() {
		var (sut, archive) = CreateSut();

		var chunkInfo = GetChunkInfo(0, 0, complete: true);
		sut.Handle(new SystemMessage.ChunkLoaded(chunkInfo));

		await WaitFor(archive, numStores: 0);

		Assert.Equal([], archive.Chunks);
	}

	[Fact]
	public async Task archives_a_switched_chunk() {
		var (sut, archive) = CreateSut();

		var chunkInfo = GetChunkInfo(0, 0, complete: true);
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(0));
		sut.Handle(new SystemMessage.ChunkSwitched(chunkInfo));

		await WaitFor(archive, numStores: 1);

		Assert.Equal(["0-0.renamed"], archive.Chunks);
	}

	[Fact]
	public async Task archives_chunks_in_order() {
		var (sut, archive) = CreateSut(chunkStorageDelay: TimeSpan.FromMilliseconds(100));

		sut.Handle(new SystemMessage.ChunkCompleted(GetChunkInfo(3, 3, complete: true)));
		sut.Handle(new SystemMessage.ChunkCompleted(GetChunkInfo(4, 4, complete: true)));

		// chunks can be loaded out of order, since they are opened in parallel
		sut.Handle(new SystemMessage.ChunkLoaded(GetChunkInfo(2, 2, complete: true)));
		sut.Handle(new SystemMessage.ChunkLoaded(GetChunkInfo(1, 1, complete: true)));
		sut.Handle(new SystemMessage.ChunkLoaded(GetChunkInfo(0, 0, complete: true)));

		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(GetChunkInfo(4, 4, complete: true).ChunkEndPosition));

		await WaitFor(archive, numStores: 5);

		Assert.Equal(["0-0.renamed", "1-1.renamed", "2-2.renamed", "3-3.renamed", "4-4.renamed"], archive.Chunks);
	}

	[Fact]
	public async Task prioritizes_archiving_of_scavenged_chunks_over_new_chunks() {
		var (sut, archive) = CreateSut(chunkStorageDelay: TimeSpan.FromMilliseconds(100));

		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(0));

		sut.Handle(new SystemMessage.ChunkCompleted(GetChunkInfo(3, 3, complete: true)));
		sut.Handle(new SystemMessage.ChunkCompleted(GetChunkInfo(4, 4, complete: true)));
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(GetChunkInfo(4, 4, complete: true).ChunkEndPosition));

		await WaitFor(archive, numStores: 1);

		sut.Handle(new SystemMessage.ChunkCompleted(GetChunkInfo(5, 5, complete: true)));
		sut.Handle(new SystemMessage.ChunkSwitched(GetChunkInfo(1, 2, complete: true)));

		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(GetChunkInfo(5, 5, complete: true).ChunkEndPosition));

		await WaitFor(archive, numStores: 5);

		Assert.Equal(["3-3.renamed", "4-4.renamed", "1-1.renamed", "2-2.renamed", "5-5.renamed"], archive.Chunks);
	}

	[Fact]
	public async Task doesnt_archive_existing_chunks_that_were_already_archived() {
		var (sut, archive) = CreateSut(existingChunks:	[ "0-0", "1-1" ], existingCheckpoint: 2 * TFConsts.ChunkSize);

		sut.Handle(new SystemMessage.ChunkLoaded(GetChunkInfo(0, 0, complete: true)));
		sut.Handle(new SystemMessage.ChunkLoaded(GetChunkInfo(1, 1, complete: true)));
		sut.Handle(new SystemMessage.ChunkLoaded(GetChunkInfo(2, 2, complete: true)));
		sut.Handle(new SystemMessage.ChunkLoaded(GetChunkInfo(3, 3, complete: true)));
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(GetChunkInfo(3, 3, complete: true).ChunkEndPosition));

		await WaitFor(archive, numStores: 2);

		Assert.Equal([ "0-0", "1-1", "2-2.renamed", "3-3.renamed" ], archive.Chunks);
	}

	[Fact]
	public async Task archives_an_existing_chunk_if_it_starts_before_but_ends_after_the_checkpoint() {
		var (sut, archive) = CreateSut(existingChunks:	[ "0-0", "1-1" ], existingCheckpoint: 2 * TFConsts.ChunkSize);

		sut.Handle(new SystemMessage.ChunkLoaded(GetChunkInfo(0, 0, complete: true)));
		sut.Handle(new SystemMessage.ChunkLoaded(GetChunkInfo(1, 2, complete: true))); // <-- the chunk being tested
		sut.Handle(new SystemMessage.ChunkLoaded(GetChunkInfo(3, 3, complete: true)));
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(GetChunkInfo(3, 3, complete: true).ChunkEndPosition));

		await WaitFor(archive, numStores: 3);

		Assert.Equal([ "0-0", "1-1", "1-1.renamed", "2-2.renamed", "3-3.renamed" ], archive.Chunks);
	}

	[Fact]
	public async Task cancels_archiving_when_system_shuts_down() {
		var (sut, archive) = CreateSut(TimeSpan.FromMilliseconds(100));

		var chunkInfo = GetChunkInfo(0, 0);
		sut.Handle(new SystemMessage.ChunkCompleted(chunkInfo));
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(chunkInfo.ChunkEndPosition));
		sut.Handle(new SystemMessage.BecomeShuttingDown(Guid.NewGuid(), exitProcess: true, shutdownHttp: true));

		await WaitFor(archive, numStores: 0);

		Assert.Equal([ ], archive.Chunks);
	}

	[Fact]
	public async Task moves_the_archive_checkpoint_forward() {
		var (sut, archive) = CreateSut();

		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(0));

		Assert.Equal(0, await archive.GetCheckpoint(CancellationToken.None));

		var chunkInfo = GetChunkInfo(0, 0);
		sut.Handle(new SystemMessage.ChunkCompleted(chunkInfo));
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(chunkInfo.ChunkEndPosition));

		await WaitFor(archive, numStores: 1, numCheckpoints: 1);
		Assert.Equal(ChunkSize, await archive.GetCheckpoint(CancellationToken.None));

		chunkInfo = GetChunkInfo(1, 1);
		sut.Handle(new SystemMessage.ChunkCompleted(chunkInfo));
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(chunkInfo.ChunkEndPosition));

		await WaitFor(archive, numStores: 2, numCheckpoints: 2);
		Assert.Equal(2 * ChunkSize, await archive.GetCheckpoint(CancellationToken.None));
	}

	[Fact]
	public async Task doesnt_move_the_archive_checkpoint_backward() {
		var (sut, archive) = CreateSut();

		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(0));

		Assert.Equal(0, await archive.GetCheckpoint(CancellationToken.None));

		var chunkInfo = GetChunkInfo(4, 4);
		sut.Handle(new SystemMessage.ChunkCompleted(chunkInfo));
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(chunkInfo.ChunkEndPosition));

		await WaitFor(archive, numStores: 1, numCheckpoints: 1);
		Assert.Equal(5 * ChunkSize, await archive.GetCheckpoint(CancellationToken.None));

		chunkInfo = GetChunkInfo(0, 2);
		sut.Handle(new SystemMessage.ChunkSwitched(chunkInfo));
		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(chunkInfo.ChunkEndPosition));

		await WaitFor(archive, numStores: 2, numCheckpoints: 1);
		Assert.Equal(5 * ChunkSize, await archive.GetCheckpoint(CancellationToken.None));
	}

	[Fact]
	public async Task unmerges_merged_chunks_when_archiving() {
		var (sut, archive) = CreateSut();

		sut.Handle(new SystemMessage.ChunkSwitched(GetChunkInfo(0, 4, complete: true)));
		sut.Handle(new SystemMessage.ChunkSwitched(GetChunkInfo(5, 5, complete: true)));
		sut.Handle(new SystemMessage.ChunkSwitched(GetChunkInfo(6, 7, complete: true)));

		sut.Handle(new ReplicationTrackingMessage.ReplicatedTo(GetChunkInfo(6, 10, complete: true).ChunkEndPosition));

		await WaitFor(archive, numStores: 8);

		Assert.Equal(["0-0.renamed", "1-1.renamed", "2-2.renamed", "3-3.renamed", "4-4.renamed", "5-5.renamed", "6-6.renamed", "7-7.renamed"],
			archive.Chunks);
	}
}

internal class FakeSubscriber : ISubscriber {
	public void Subscribe<T>(IAsyncHandle<T> handler) where T : Message { }
	public void Unsubscribe<T>(IAsyncHandle<T> handler) where T : Message { }
}

internal class FakeUnmerger : IChunkUnmerger {
	public async IAsyncEnumerable<string> Unmerge(string chunkPath, int chunkStartNumber, int chunkEndNumber) {
		await Task.Delay(TimeSpan.Zero);
		for (var i = chunkStartNumber; i <= chunkEndNumber; i++) {
			yield return $"{i}-{i}";
		}
	}
}

internal class FakeArchiveChunkNamer : IArchiveChunkNamer {
	public string Prefix => "";

	public string GetFileNameFor(int logicalChunkNumber) => $"{logicalChunkNumber}-{logicalChunkNumber}.renamed";
}

internal class FakeArchiveStorage : IArchiveStorageWriter, IArchiveStorageReader, IArchiveStorageFactory {
	public List<string> Chunks;
	public int NumStores => Interlocked.CompareExchange(ref _stores, 0, 0);
	private int _stores;

	private readonly TimeSpan _chunkStorageDelay;
	private readonly string[] _existingChunks;

	public int NumCheckpoints { get; private set; }
	private long _checkpoint;

	public FakeArchiveStorage(TimeSpan chunkStorageDelay, string[] existingChunks, long existingCheckpoint) {
		_chunkStorageDelay = chunkStorageDelay;
		_existingChunks = existingChunks;
		_checkpoint = existingCheckpoint;
		Chunks = new List<string>(existingChunks);
	}

	public IArchiveChunkNamer ChunkNamer => throw new NotImplementedException();

	public IArchiveStorageReader CreateReader() => this;
	public IArchiveStorageWriter CreateWriter() => this;

	public async ValueTask<bool> StoreChunk(string chunkPath, string destinationFile, CancellationToken ct) {
		await Task.Delay(_chunkStorageDelay, ct);
		Chunks.Add(destinationFile);
		Interlocked.Increment(ref _stores);
		return true;
	}

	public ValueTask<long> GetCheckpoint(CancellationToken ct) {
		return ValueTask.FromResult(_checkpoint);
	}

	public ValueTask<bool> SetCheckpoint(long checkpoint, CancellationToken ct) {
		_checkpoint = checkpoint;
		NumCheckpoints++;
		return ValueTask.FromResult(true);
	}

	public ValueTask<Stream> GetChunk(string chunkFile, CancellationToken ct) {
		throw new NotImplementedException();
	}

	public ValueTask<Stream> GetChunk(string chunkFile, long start, long end, CancellationToken ct) {
		throw new NotImplementedException();
	}

	public IAsyncEnumerable<string> ListChunks(CancellationToken ct) {
		return _existingChunks.ToAsyncEnumerable();
	}
}
