// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive.Naming;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Naming;

public class ArchiveChunkNameResolverTests {
	private static ArchiveChunkNameResolver CreateSut() {
		var namingStrategy = new VersionedPatternFileNamingStrategy(string.Empty, "chunk-");
		return new ArchiveChunkNameResolver(namingStrategy);
	}

	[Theory]
	[InlineData(0, "chunk-000000.000001")]
	[InlineData(1, "chunk-000001.000001")]
	[InlineData(1000, "chunk-001000.000001")]
	public async Task returns_correct_file_name(int logicalChunkNumber, string expectedFileName) {
		var sut = CreateSut();
		Assert.Equal(expectedFileName, await sut.GetFileNameFor(logicalChunkNumber));
	}

	[Fact]
	public async Task throws_if_chunk_number_is_negative() {
		var sut = CreateSut();
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await sut.GetFileNameFor(-1));
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await sut.GetFileNameFor(-2));
	}

	[Fact]
	public void returns_correct_prefix() {
		var sut = CreateSut();
		Assert.Equal("chunk-", sut.Prefix);
	}
}
