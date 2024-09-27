// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.ByteDup;
public class ByteDupChunkWriteStream(ChunkDataWriteStream stream) :
	ChunkDataWriteStream(stream.ChunkFileStream, stream.ChecksumAlgorithm) {
	private const int HeaderSize = 128;
	public override void Write(byte[] buffer, int offset, int count) {
		var buf = new byte[count * 2];
		for (int i = 0; i < count; i++)
			buf[i * 2] = buf[i * 2 + 1] = buffer[i + offset];

		ChunkFileStream.Write(buf, 0, buf.Length);
		ChecksumAlgorithm.TransformBlock(buf, 0, buf.Length, null, 0);
	}

	private static long TransformPosition(long position) => HeaderSize + (position - HeaderSize) * 2L;
	private static long UntransformPosition(long position) => HeaderSize + (position - HeaderSize) / 2L;
	public override long Position {
		get => UntransformPosition(base.Position);
		set => base.Position = TransformPosition(value);
	}

	public override void SetLength(long value) => ChunkFileStream.SetLength(TransformPosition(value));
	public override long Length => UntransformPosition(ChunkFileStream.Length);
}
