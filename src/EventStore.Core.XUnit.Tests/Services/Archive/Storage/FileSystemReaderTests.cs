// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Storage;

public class FileSystemReaderTests : ArchiveStorageTestsBase<FileSystemReaderTests> {
	[Fact]
	public async Task can_list_chunks() {
		var sut = CreateReaderSut();

		Assert.Equal(0, await sut.ListChunks(CancellationToken.None).CountAsync());

		var chunk0 = Path.GetFileName(CreateArchiveChunk(0, 0));
		var chunk1 = Path.GetFileName(CreateArchiveChunk(1, 0));
		var chunk2 = Path.GetFileName(CreateArchiveChunk(2, 0));

		var archivedChunks = sut.ListChunks(CancellationToken.None).ToEnumerable();
		Assert.Equal([chunk0, chunk1, chunk2], archivedChunks);
	}
}
