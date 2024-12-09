// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using EventStore.Core.TransactionLog.Checkpoint;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services.Archive.ArchiveCatchup;

using ArchiveCatchup = Core.Services.Archive.ArchiveCatchup.ArchiveCatchup;

public class ArchiveCatchupTests : DirectoryPerTest<ArchiveCatchupTests> {
	private const int ChunkSize = 1000;
	private string DbPath => Path.Combine(Fixture.Directory, "db");

	public ArchiveCatchupTests() {
		Directory.CreateDirectory(DbPath);
	}

	private struct Sut {
		public ArchiveCatchup Catchup { get; init; }
		public FakeArchiveStorage Archive { get; init; }
		public string[] DbChunks { get; init; }
		public string[] ArchiveChunks { get; init; }
		public ICheckpoint WriterCheckpoint { get; init; }
		public ICheckpoint ReplicationCheckpoint { get; init; }
	}

	private Sut CreateSut(
		long? dbCheckpoint = null,
		long? archiveCheckpoint = null,
		string[] dbChunks = null,
		string[] archiveChunks = null,
		Action<string> onGetChunk = null) {
		dbCheckpoint ??= 0L;
		archiveCheckpoint ??= 0L;
		dbChunks ??= CreateChunkList(dbCheckpoint.Value);
		archiveChunks ??= CreateChunkList(archiveCheckpoint.Value);

		foreach (var dbChunk in dbChunks) {
			using var _ = File.CreateText(Path.Combine(DbPath, dbChunk));
		}

		var archive = new FakeArchiveStorage(
			chunkSize: ChunkSize,
			archiveChunks,
			archiveCheckpoint.Value,
			onGetChunk);

		var writerCheckpoint = new InMemoryCheckpoint(dbCheckpoint.Value);
		var replicationCheckpoint = new InMemoryCheckpoint(dbCheckpoint.Value);
		var catchup = new ArchiveCatchup(
			dbPath: DbPath,
			writerCheckpoint: writerCheckpoint,
			replicationCheckpoint: replicationCheckpoint,
			chunkSize: ChunkSize,
			fileNamingStrategy: new CustomNamingStrategy(),
			archiveStorageFactory: archive
		);

		return new Sut {
			Catchup = catchup,
			Archive = archive,
			DbChunks = dbChunks,
			ArchiveChunks = archiveChunks,
			WriterCheckpoint = writerCheckpoint,
			ReplicationCheckpoint = replicationCheckpoint
		};
	}

	private string[] CreateChunkList(long checkpoint) {
		var chunks = new List<string>();
		var namingStrategy = new CustomNamingStrategy();

		var numChunks = checkpoint / ChunkSize;
		if (checkpoint % ChunkSize != 0)
			numChunks++;

		for (var i = 0; i < numChunks; i++)
			chunks.Add(namingStrategy.GetFilenameFor(i, i));

		return chunks.ToArray();
	}

	[Theory]
	[InlineData(0L, 0L)]       // db is in sync with archive, both have no data
	[InlineData(1000L, 1000L)] // db is in sync with archive, both have one chunk
	[InlineData(3000L, 3000L)] // db is in sync with archive, both have multiple chunks
	[InlineData(1005L, 0L)]    // db is ahead of archive, archive is empty
	[InlineData(1005L, 1000L)] // db is ahead of archive by some data
	[InlineData(4000L, 1000L)] // db is ahead of archive by multiple chunks
	[InlineData(4005L, 1000L)] // db is ahead of archive by multiple chunks & some data
	public async Task doesnt_catch_up_if_db_is_ahead_or_in_sync_with_archive(long dbCheckpoint, long archiveCheckpoint) {
		var sut = CreateSut(dbCheckpoint: dbCheckpoint, archiveCheckpoint: archiveCheckpoint);

		await sut.Catchup.Run();

		Assert.Equal(0, sut.Archive.NumListings);
		Assert.Empty(sut.Archive.ChunkGets);
		Assert.Equal(dbCheckpoint, sut.WriterCheckpoint.Read());
		Assert.Equal(dbCheckpoint, sut.ReplicationCheckpoint.Read());
		Assert.Equal(archiveCheckpoint, await sut.Archive.GetCheckpoint(CancellationToken.None));
	}

	[Theory]
	[InlineData(0L, 1000L)]    // db is empty, archive has a single chunk
	[InlineData(0L, 3000L)]    // db is empty, archive has multiple chunks
	[InlineData(5L, 1000L)]    // db has a single, incomplete, chunk, archive has a single chunk
	[InlineData(5L, 3000L)]    // db has a single, incomplete, chunk, archive has multiple chunks
	[InlineData(2005L, 5000L)] // db has multiple chunks (including an incomplete one), archive has multiple chunks
	[InlineData(2000L, 5000L)] // db has multiple complete chunks, archive has multiple chunks
	[InlineData(3000L, 4000L)] // db is exactly one chunk behind archive
	[InlineData(3005L, 4000L)] // db is less than one chunk behind archive
	public async Task catches_up_if_db_is_behind_archive_1(long dbCheckpoint, long archiveCheckpoint) {
		var sut = CreateSut(
			dbCheckpoint: dbCheckpoint,
			archiveCheckpoint: archiveCheckpoint);

		await sut.Catchup.Run();

		var chunksToGet = new List<string>();
		for (var i = (int)(dbCheckpoint / ChunkSize); i < (int)(archiveCheckpoint / ChunkSize); i++)
			chunksToGet.Add($"chunk-{i}.{i}");

		var chunksToBackup = new List<string>();
		if (dbCheckpoint % ChunkSize != 0)
			chunksToBackup.Add($"{sut.DbChunks[dbCheckpoint / ChunkSize]}.archive.bkup");

		await VerifyCatchUp(sut, dbCheckpoint, archiveCheckpoint, chunksToGet.ToArray(), chunksToBackup.ToArray());
	}

	[Theory]
	// there are no chunks in archive with start pos >= writer checkpoint, db has one incomplete chunk:
	[InlineData(5L, 4000L, new[] {"chunk-0.0"}, new[] {"chunk-0.3"}, new[] {"chunk-0.3"}, new string [] { })]
	// there are no chunks in archive with start pos >= writer checkpoint, db has one complete chunk:
	[InlineData(1000L, 4000L, new[] {"chunk-0.0"}, new[] {"chunk-0.3"}, new[] {"chunk-0.3"}, new string [] { })]
	// there exists a chunk in archive (chunk-2.3) with start pos > writer checkpoint:
	[InlineData(1005L, 5000L, new[] {"chunk-0.0", "chunk-1.1"}, new[] {"chunk-0.0", "chunk-1.1", "chunk-2.3", "chunk-3.4"}, new[] {"chunk-1.1", "chunk-2.3", "chunk-3.4"}, new[] { "chunk-1.1.archive.bkup" })]
	// there exists a chunk in archive (chunk-4.4) with start pos > writer checkpoint:
	[InlineData(3000L, 6000L, new[] {"chunk-0.2"}, new[] {"chunk-0.4", "chunk-4.4", "chunk-5.5"}, new[] {"chunk-0.4", "chunk-4.4", "chunk-5.5"}, new string [] { })]
	// there exists a chunk in archive (chunk-0.3) with start pos == writer checkpoint, db is empty:
	[InlineData(0L, 4000L, new string[] { }, new[] {"chunk-0.3"}, new[] {"chunk-0.3"}, new string [] { })]
	// there exists a chunk in archive (chunk-2.3) with start pos == writer checkpoint:
	[InlineData(2000L, 5000L, new[] {"chunk-0.0", "chunk-1.1"}, new[] {"chunk-0.0", "chunk-1.1", "chunk-2.3", "chunk-3.4"}, new[] {"chunk-2.3", "chunk-3.4"}, new string [] { })]
	public async Task catches_up_if_db_is_behind_archive_2(
		long dbCheckpoint,
		long archiveCheckpoint,
		string[] dbChunks,
		string[] archiveChunks,
		string[] expectedChunksToFetch,
		string[] expectedChunksToBackup) {

		var sut = CreateSut(
			dbCheckpoint: dbCheckpoint,
			archiveCheckpoint: archiveCheckpoint,
			dbChunks: dbChunks,
			archiveChunks: archiveChunks);

		await sut.Catchup.Run();
		await VerifyCatchUp(sut, dbCheckpoint, archiveCheckpoint, expectedChunksToFetch, expectedChunksToBackup);
	}

	[Fact]
	public async Task retries_if_a_chunk_is_deleted_from_the_archive_when_catching_up() {
		// scenario: there is an ongoing scavenge on the archive, so while catching up,
		// chunks in the archive listing may no longer be present.

		bool exceptionThrown = false;

		var sut = CreateSut(
			dbCheckpoint: 0L,
			archiveCheckpoint: 2000L,
			archiveChunks: new [] { "chunk-0.0", "chunk-1.1" },
			onGetChunk: OnGetChunk);

		await sut.Catchup.Run();

		Assert.Equal(2, sut.Archive.NumListings);
		Assert.Equal(2000L, sut.WriterCheckpoint.Read());
		Assert.Equal(2000L, sut.ReplicationCheckpoint.Read());

		return;

		// we just want to test retries here. for simplicity, we just throw an exception the first time chunk-1.1 is
		// fetched, then we don't throw on the second fetch. but in practice, it would be slightly different: the
		// archive's directory listing would change on retry.
		void OnGetChunk(string chunkFile) {
			if (chunkFile == "chunk-1.1" && !exceptionThrown) {
				exceptionThrown = true;
				throw new ChunkDeletedException();
			}
		}
	}

	private async Task VerifyCatchUp(Sut sut, long dbCheckpoint, long archiveCheckpoint, string[] expectedChunkGets, string[] expectedChunkBackups) {
		// lists archive chunks
		Assert.Equal(1, sut.Archive.NumListings);

		// fetches the expected chunks
		Assert.Equal(expectedChunkGets, sut.Archive.ChunkGets);

		// moves the writer checkpoint
		Assert.Equal(archiveCheckpoint, sut.WriterCheckpoint.Read());

		// moves the replication checkpoint
		Assert.Equal(archiveCheckpoint, sut.ReplicationCheckpoint.Read());

		// doesn't move the archive checkpoint
		Assert.Equal(archiveCheckpoint, await sut.Archive.GetCheckpoint(CancellationToken.None));

		// backs up the expected chunks
		Assert.Equal(expectedChunkBackups, ListBackedUpChunks());
	}

	private IEnumerable<string> ListBackedUpChunks() {
		return Directory.EnumerateFiles(DbPath)
			.Where(x => x.EndsWith(".bkup"))
			.Select(Path.GetFileName)
			.Order();
	}
}

