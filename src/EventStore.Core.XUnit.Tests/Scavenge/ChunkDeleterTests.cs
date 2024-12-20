// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Tests.Index.Hashers;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.TransactionLog.Scavenging;
using EventStore.Core.TransactionLog.Scavenging.InMemory;
using EventStore.Core.TransactionLog.Scavenging.Interfaces;
using EventStore.Core.TransactionLog.Scavenging.Stages;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Scavenge;

public class ChunkDeleterTests {
	readonly IScavengeStateBackend<string> _backend;
	readonly ScavengeStateForChunkWorker<string> _scavengeState;

	public ChunkDeleterTests() {
		_backend = new InMemoryScavengeBackend();
		_scavengeState = new ScavengeStateForChunkWorker<string>(
			hasher: new HumanReadableHasher(),
			backend: _backend,
			collisions: [],
			onDispose: () => { });
	}

	static ChunkDeleter<string, ILogRecord> GenSut(
		int retainDays,
		long retainBytes,
		Func<long> readArchiveCheckpoint,
		int maxAttempts = 1) {

		var sut = new ChunkDeleter<string, ILogRecord>(
			logger: Serilog.Log.Logger,
			archiveCheckpoint: new AdvancingCheckpoint(_ => new(readArchiveCheckpoint())),
			retainPeriod: TimeSpan.FromDays(retainDays),
			retainBytes: retainBytes,
			maxAttempts: maxAttempts,
			retryDelayMs: 0);
		return sut;
	}

	static ScavengePoint GenScavengePoint(long position, DateTime effectiveNow) {
		var scavengePoint = new ScavengePoint(
			position: position,
			eventNumber: 0,
			effectiveNow: effectiveNow,
			threshold: 0);
		return scavengePoint;
	}

	public enum ExpectedOutcome {
		Deleted,
		Retained,
		ExceptionNotPresent,
	}

	[Theory]
	[InlineData(1000, 10, true, ExpectedOutcome.Deleted, "can be deleted")]
	[InlineData(1000, 10, false, ExpectedOutcome.ExceptionNotPresent, "exception when not present in archive")]
	[InlineData(1000, 11, true, ExpectedOutcome.Retained, "retain because of retention period (in archive)")]
	[InlineData(1000, 11, false, ExpectedOutcome.Retained, "retain because of retention period (not in archive)")]
	[InlineData(1001, 10, true, ExpectedOutcome.Retained, "retain because of retention bytes (in archive)")]
	[InlineData(1001, 10, false, ExpectedOutcome.Retained, "retain because of retention bytes (not in archive)")]
	[InlineData(1001, 11, true, ExpectedOutcome.Retained, "retain because of both (in archive)")]
	[InlineData(1001, 11, false, ExpectedOutcome.Retained, "retain because of both (not in archive)")]
	public async Task simple_cases(
		int retainBytes,
		int retainDays,
		bool isInArchive,
		ExpectedOutcome expectedOutcome,
		string name) {

		_ = name;
		var minDateInChunk = new DateTime(2024, 1, 1);
		var maxDateInChunk = new DateTime(2024, 12, 1);
		_backend.ChunkTimeStampRanges[0] = new(minDateInChunk, maxDateInChunk);

		// chunk 1 contains records with positions 1000-2000
		var chunk = new FakeChunk(chunkStartNumber: 0, chunkEndNumber: 1, chunkSize: 1_000);

		var sut = GenSut(
			retainDays: retainDays,
			retainBytes: retainBytes,
			readArchiveCheckpoint: () => isInArchive
				? chunk.ChunkEndPosition
				: chunk.ChunkEndPosition - 1);

		var when = async () => {
			var deleted = await sut.DeleteIfNotRetained(
				scavengePoint: GenScavengePoint(
					// scavenging > 1000 bytes after the end of the chunk
					position: chunk.ChunkEndPosition + 1001,
					// scavenging > 10 days after the last record in the chunk
					effectiveNow: maxDateInChunk + TimeSpan.FromDays(10.1)),
				concurrentState: _scavengeState,
				physicalChunk: chunk,
				CancellationToken.None);
			return deleted;
		};

		if (expectedOutcome == ExpectedOutcome.ExceptionNotPresent) {
			var ex = await Assert.ThrowsAsync<Exception>(when);
			Assert.Contains("Chunk 1 is not yet present in the archive", ex.Message);
			return;
		}

		var deleted = await when();
		if (expectedOutcome == ExpectedOutcome.Deleted)
			Assert.True(deleted);
		else if (expectedOutcome == ExpectedOutcome.Retained)
			Assert.False(deleted);
		else
			throw new InvalidOperationException();
	}

	[Fact]
	public async Task retries_if_not_yet_present_in_archive() {
		var minDateInChunk = new DateTime(2024, 1, 1);
		var maxDateInChunk = new DateTime(2024, 12, 1);
		_backend.ChunkTimeStampRanges[0] = new(minDateInChunk, maxDateInChunk);

		// chunk 0-1 contains records with positions 1000-2000
		var chunk = new FakeChunk(chunkStartNumber: 0, chunkEndNumber: 1, chunkSize: 1_000);

		var attempts = 0;

		var sut = GenSut(
			retainDays: 10,
			retainBytes: 1000,
			readArchiveCheckpoint: () => {
				attempts++;
				// not present in archive
				return chunk.ChunkEndPosition - 1;
			},
			maxAttempts: 3);

		var ex = await Assert.ThrowsAsync<Exception>(async () =>
			await sut.DeleteIfNotRetained(
				scavengePoint: GenScavengePoint(
					// scavenging > 1000 bytes after the end of the chunk
					position: chunk.ChunkEndPosition + 1001,
					// scavenging > 10 days after the last record in the chunk
					effectiveNow: maxDateInChunk + TimeSpan.FromDays(10.1)),
				concurrentState: _scavengeState,
				physicalChunk: chunk,
				CancellationToken.None));

		Assert.Contains("Chunk 1 is not yet present in the archive", ex.Message);
		Assert.Equal(3, attempts);
	}

	[Fact]
	public async Task when_unexpected_error_accessing_archive() {
		var minDateInChunk = new DateTime(2024, 1, 1);
		var maxDateInChunk = new DateTime(2024, 12, 1);
		_backend.ChunkTimeStampRanges[1] = new(minDateInChunk, maxDateInChunk);

		// chunk 1 contains records with positions 1000-2000
		var chunk = new FakeChunk(chunkStartNumber: 1, chunkEndNumber: 1, chunkSize: 1_000);

		var sut = GenSut(
			retainDays: 10,
			retainBytes: 1000,
			readArchiveCheckpoint: () => throw new Exception("something happened"));

		var ex = await Assert.ThrowsAsync<Exception>(async () =>
			await sut.DeleteIfNotRetained(
				scavengePoint: GenScavengePoint(
					// scavenging > 1000 bytes after the end of the chunk
					position: chunk.ChunkEndPosition + 1001,
					// scavenging > 10 days after the last record in the chunk
					effectiveNow: maxDateInChunk + TimeSpan.FromDays(10.1)),
				concurrentState: _scavengeState,
				physicalChunk: chunk,
				CancellationToken.None));

		Assert.Contains("something happened", ex.Message);
	}

	[Fact]
	public async Task when_chunk_has_no_prepares() {
		// chunk 1 contains records with positions 1000-2000
		var chunk = new FakeChunk(chunkStartNumber: 1, chunkEndNumber: 1, chunkSize: 1_000);

		var sut = GenSut(
			retainDays: 10,
			retainBytes: 1000,
			readArchiveCheckpoint: () =>
				// present in archive
				chunk.ChunkEndPosition);

		var deleted = await sut.DeleteIfNotRetained(
			scavengePoint: GenScavengePoint(
				// scavenging > 1000 bytes after the end of the chunk
				position: chunk.ChunkEndPosition + 1001,
				// doesn't matter when, no dates are populated in the ChunkTimeStampRanges
				effectiveNow: DateTime.Now),
			concurrentState: _scavengeState,
			physicalChunk: chunk,
			CancellationToken.None);

		Assert.True(deleted);
	}

	class FakeChunk(
		int chunkStartNumber,
		int chunkEndNumber,
		int chunkSize = 1_000)

		: IChunkReaderForExecutor<string, ILogRecord> {
		public string Name => $"Chunk {chunkStartNumber}-{chunkEndNumber}";

		public int FileSize => throw new NotImplementedException();

		public int ChunkStartNumber => chunkStartNumber;

		public int ChunkEndNumber => chunkEndNumber;

		public bool IsReadOnly => throw new NotImplementedException();

		public bool IsRemote => throw new NotImplementedException();

		public long ChunkStartPosition => chunkStartNumber * chunkSize;

		public long ChunkEndPosition => (chunkEndNumber + 1) * chunkSize;

		public IAsyncEnumerable<bool> ReadInto(
			RecordForExecutor<string, ILogRecord>.NonPrepare nonPrepare,
			RecordForExecutor<string, ILogRecord>.Prepare prepare,
			CancellationToken token) {
			throw new NotImplementedException();
		}
	}
}
