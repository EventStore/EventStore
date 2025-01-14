// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive.Naming;
using EventStore.Core.Services.Archive.Storage;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Plugins.Transforms;

namespace EventStore.Core.XUnit.Tests.Services.Archive.ArchiveCatchup;

internal class FakeArchiveStorage : IArchiveStorage {
	private readonly int _chunkSize;
	private readonly long _checkpoint;

	private readonly Action<int> _onGetChunk;

	public int[] ChunkGets {
		get {
			lock (_chunkGets) {
				return _chunkGets.Order().ToArray();
			}
		}
	}
	private readonly List<int> _chunkGets;
	private readonly IArchiveChunkNameResolver _chunkNameResolver;


	public FakeArchiveStorage(int chunkSize, long checkpoint, Action<int> onGetChunk, IArchiveChunkNameResolver chunkNameResolver) {
		_chunkSize = chunkSize;
		_checkpoint = checkpoint;
		_onGetChunk = onGetChunk;
		_chunkGets = new();
		_chunkNameResolver = chunkNameResolver;
	}

	public ValueTask<bool> StoreChunk(string chunkPath, int logicalChunkNumber, CancellationToken ct) => throw new NotImplementedException();
	public ValueTask<bool> SetCheckpoint(long checkpoint, CancellationToken ct) => throw new NotImplementedException();

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

	public ValueTask<int> ReadAsync(int logicalChunkNumber, Memory<byte> buffer, long offset, CancellationToken ct) {
		lock (_chunkGets) {
			_chunkGets.Add(logicalChunkNumber);
		}

		_onGetChunk?.Invoke(logicalChunkNumber);
		var chunk = new byte[ChunkHeader.Size + _chunkSize];
		var header = CreateChunkHeader(logicalChunkNumber, logicalChunkNumber);
		header.Format(chunk.AsSpan()[..ChunkHeader.Size]);

		chunk.CopyTo(buffer);
		return new(buffer.Length);
	}

	public ValueTask<ArchivedChunkMetadata> GetMetadataAsync(int logicalChunkNumber, CancellationToken token) {
		throw new NotImplementedException();
	}
}
