using System;
using System.IO;
using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.WithHeader;

public class WithHeaderChunkReadStream(ChunkDataReadStream stream, int transformHeaderSize)
	: ChunkDataReadStream(stream.ChunkFileStream) {

	public override long Seek(long offset, SeekOrigin origin)
	{
		if (origin != SeekOrigin.Begin)
			throw new NotSupportedException();
		Position = offset;
		return offset;
	}

	public override long Position {
		get => ChunkFileStream.Position - transformHeaderSize;
		set => ChunkFileStream.Position = value + transformHeaderSize;
	}
}
