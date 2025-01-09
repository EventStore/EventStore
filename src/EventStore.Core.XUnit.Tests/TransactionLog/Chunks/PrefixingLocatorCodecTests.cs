// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.TransactionLog.Chunks.TFChunk;
using Xunit;

namespace EventStore.Core.XUnit.Tests.TransactionLog.Chunks;

public class PrefixingLocatorCodecTests {
	private readonly PrefixingLocatorCodec _sut = new();

	[Fact]
	public void can_encode_local() {
		Assert.Equal("chunk-123.456", _sut.EncodeLocalName("chunk-123.456"));
	}

	[Fact]
	public void can_encode_remote() {
		Assert.Equal("archive:chunk-123.456", _sut.EncodeRemoteName("chunk-123.456"));
	}

	[Fact]
	public void can_decode_local() {
		Assert.False(_sut.Decode("chunk-123.456", out var decoded));
		Assert.Equal("chunk-123.456", decoded);
	}

	[Fact]
	public void can_decode_remote() {
		Assert.True(_sut.Decode("archive:chunk-123.456", out var decoded));
		Assert.Equal("chunk-123.456", decoded);
	}
}
