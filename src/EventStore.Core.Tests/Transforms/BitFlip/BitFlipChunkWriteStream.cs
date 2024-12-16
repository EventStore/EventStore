// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Buffers;
using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.BitFlip;

public class BitFlipChunkWriteStream(ChunkDataWriteStream stream) :
	ChunkDataWriteStream(stream.ChunkFileStream, stream.ChecksumAlgorithm) {

	public override void Write(ReadOnlySpan<byte> buffer) {
		var tmp = new byte[buffer.Length];
		FlipBits(buffer, tmp);

		base.Write(tmp);
	}

	public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default) {
		var tmp = new byte[buffer.Length];
		FlipBits(buffer.Span, tmp);

		return base.WriteAsync(tmp, token);
	}

	private static void FlipBits(ReadOnlySpan<byte> source, Span<byte> destination) {
		for (int i = 0; i < source.Length; i++)
			destination[i] = (byte)(source[i] ^ 0xFF);
	}
}
