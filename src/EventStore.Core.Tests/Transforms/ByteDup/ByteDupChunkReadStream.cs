using System;
using System.IO;
using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.ByteDup;

public class ByteDupChunkReadStream(ChunkDataReadStream stream)
	: ChunkDataReadStream(stream.ChunkFileStream) {
	private const int HeaderSize = 128;

	public override int Read(byte[] buffer, int offset, int count) {
		var buf = new byte[count * 2];
		int numRead = base.Read(buf, 0, buf.Length);
		for (int i = 0; i < count; i++)
			buffer[i + offset] = buf[i * 2];

		return numRead / 2;
	}

	public override long Seek(long offset, SeekOrigin origin) {
		if (origin != SeekOrigin.Begin)
			throw new NotSupportedException();

		Position = offset;
		return offset;
	}

	public override long Position {
		get => HeaderSize + (base.Position - HeaderSize) / 2L;
		set => base.Position = HeaderSize + (value - HeaderSize) * 2L;
	}
}
