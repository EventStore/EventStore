// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.BitFlip;

public class BitFlipChunkReadStream(ChunkDataReadStream stream)
	: ChunkDataReadStream(stream.ChunkFileStream) {
	public override int Read(byte[] buffer, int offset, int count) {
		int numRead = base.Read(buffer, offset, count);
		for (int i = 0; i < count; i++)
			buffer[i + offset] ^= 0xFF;

		return numRead;
	}
}
