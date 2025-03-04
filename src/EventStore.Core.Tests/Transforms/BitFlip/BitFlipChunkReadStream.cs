// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.BitFlip;

public class BitFlipChunkReadStream(ChunkDataReadStream stream)
	: ChunkDataReadStream(stream.ChunkFileStream) {
	public override int Read(Span<byte> buffer) {
		int numRead = base.Read(buffer);
		for (int i = 0; i < buffer.Length; i++)
			buffer[i] ^= 0xFF;

		return numRead;
	}
}
