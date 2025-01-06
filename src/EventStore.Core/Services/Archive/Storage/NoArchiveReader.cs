// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive.Naming;
using EventStore.Core.Services.Archive.Storage.Exceptions;

namespace EventStore.Core.Services.Archive.Storage;

// Reader for when there is no archive
public class NoArchiveReader : IArchiveStorageReader {
	private NoArchiveReader() { }

	public static NoArchiveReader Instance { get; } = new();

	public IArchiveChunkNamer ChunkNamer => new NoNamer();

	public ValueTask<long> GetCheckpoint(CancellationToken ct) =>
		ValueTask.FromResult<long>(0);

	public ValueTask<Stream> GetChunk(string chunkFile, CancellationToken ct) =>
		ValueTask.FromException<Stream>(new ChunkDeletedException());

	public ValueTask<Stream> GetChunk(string chunkFile, long start, long end, CancellationToken ct) =>
		ValueTask.FromException<Stream>(new ChunkDeletedException());

	public IAsyncEnumerable<string> ListChunks(CancellationToken ct) =>
		AsyncEnumerable.Empty<string>();

	// There is no archive so it doesn't matter how we would name the chunks in it.
	class NoNamer : IArchiveChunkNamer {
		public string Prefix => "chunk-";
		public string GetFileNameFor(int logicalChunkNumber) => $"{Prefix}{logicalChunkNumber}";
	}
}
