// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive.Storage;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Storage;

public class NoArchiveReaderTests {
	private readonly NoArchiveReader _sut = NoArchiveReader.Instance;

	[Fact]
	public async Task checkpoint_is_zero() {
		Assert.Equal(0, await _sut.GetCheckpoint(CancellationToken.None));
	}

	[Fact]
	public async Task get_chunk_throws_chunk_deleted_exception() {
		await Assert.ThrowsAsync<ChunkDeletedException>(async () => {
			await _sut.GetChunk("a-chunk", CancellationToken.None);
		});
	}

	[Fact]
	public async Task get_partial_chunk_throws_chunk_deleted_exception() {
		await Assert.ThrowsAsync<ChunkDeletedException>(async () => {
			await _sut.GetChunk("a-chunk", 3, 4, CancellationToken.None);
		});
	}

	[Fact]
	public async Task list_chunks_returns_empty() {
		Assert.Empty(await _sut.ListChunks(CancellationToken.None).ToArrayAsync());
	}
}
