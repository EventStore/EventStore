// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive.Naming;
using EventStore.Core.Services.Archive.Storage.Exceptions;

namespace EventStore.Core.Services.Archive.Storage;

// Reader for when there is no archive
public class NoArchiveReader : IArchiveStorageReader {
	private NoArchiveReader() { }

	public static NoArchiveReader Instance { get; } = new();

	public IArchiveChunkNameResolver ChunkNameResolver { get; } = new NoNamer();

	public ValueTask<long> GetCheckpoint(CancellationToken ct) =>
		ValueTask.FromResult<long>(0);

	public ValueTask<int> ReadAsync(int logicalChunkNumber, Memory<byte> buffer, long offset, CancellationToken ct) =>
		ValueTask.FromException<int>(new ChunkDeletedException());

	public ValueTask<ArchivedChunkMetadata> GetMetadataAsync(int logicalChunkNumber, CancellationToken token) =>
		ValueTask.FromException<ArchivedChunkMetadata>(new ChunkDeletedException());

	// There is no archive so it doesn't matter how we would name the chunks in it.
	class NoNamer : IArchiveChunkNameResolver {
		public string Prefix => "chunk-";

		public string ResolveFileName(int logicalChunkNumber) =>
			$"{Prefix}{logicalChunkNumber}";

		public int ResolveChunkNumber(string fileName) => 0;
	}
}
