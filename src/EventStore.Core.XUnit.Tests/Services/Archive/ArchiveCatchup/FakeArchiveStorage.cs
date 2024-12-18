// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive.Storage;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Plugins.Transforms;

namespace EventStore.Core.XUnit.Tests.Services.Archive.ArchiveCatchup;

internal class FakeArchiveStorage : IArchiveStorageWriter, IArchiveStorageReader, IArchiveStorageFactory {
	private readonly int _chunkSize;
	private readonly string[] _chunks;
	private readonly long _checkpoint;
	private readonly CustomNamingStrategy _customNamingStrategy = new();

	public int NumListings => Interlocked.CompareExchange(ref _listings, 0, 0);
	private int _listings;

	private readonly Action<string> _onGetChunk;

	public string[] ChunkGets {
		get {
			lock (_chunkGets) {
				return _chunkGets.Order().ToArray();
			}
		}
	}
	private readonly List<string> _chunkGets;


	public FakeArchiveStorage(int chunkSize, string[] chunks, long checkpoint, Action<string> onGetChunk) {
		_chunkSize = chunkSize;
		_chunks = chunks;
		_checkpoint = checkpoint;
		_onGetChunk = onGetChunk;
		_chunkGets = new();
	}

	public IArchiveStorageReader CreateReader() => this;
	public IArchiveStorageWriter CreateWriter() => this;

	public ValueTask<bool> StoreChunk(string chunkPath, string destinationFile, CancellationToken ct) => throw new NotImplementedException();
	public ValueTask<bool> SetCheckpoint(long checkpoint, CancellationToken ct) => throw new NotImplementedException();
	public ValueTask<Stream> GetChunk(string chunkFile, long start, long end, CancellationToken ct) => throw new NotImplementedException();

	public ValueTask<long> GetCheckpoint(CancellationToken ct) {
		return ValueTask.FromResult(_checkpoint);
	}

	private ChunkHeader CreateChunkHeader(int chunkStartNumber, int chunkEndNumber) {
		return new ChunkHeader(
			version: (int) TFChunk.ChunkVersions.Transformed,
			minCompatibleVersion: (int) TFChunk.ChunkVersions.Transformed,
			chunkSize: _chunkSize,
			chunkStartNumber,
			chunkEndNumber,
			isScavenged: false,
			chunkId: Guid.NewGuid(),
			transformType: TransformType.Identity);
	}

	public ValueTask<Stream> GetChunk(string chunkFile, CancellationToken ct) {
		lock (_chunkGets) {
			_chunkGets.Add(chunkFile);
		}

		_onGetChunk?.Invoke(chunkFile);
		var chunk = new byte[ChunkHeader.Size + _chunkSize];
		var chunkStartNumber = _customNamingStrategy.GetIndexFor(chunkFile);
		var chunkEndNumber = _customNamingStrategy.GetVersionFor(chunkFile);
		var header = CreateChunkHeader(chunkStartNumber, chunkEndNumber);
		header.Format(chunk.AsSpan()[..ChunkHeader.Size]);

		var stream = new MemoryStream(chunk);
		return ValueTask.FromResult((Stream)stream);
	}

	public IAsyncEnumerable<string> ListChunks(CancellationToken ct) {
		Interlocked.Increment(ref _listings);
		return _chunks.ToAsyncEnumerable();
	}
}
