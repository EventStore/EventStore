using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.WithHeader;

public class WithHeaderChunkWriteStream(ChunkDataWriteStream stream, int transformHeaderSize) :
	ChunkDataWriteStream(stream.ChunkFileStream, stream.ChecksumAlgorithm) {
	public override long Position {
		get => ChunkFileStream.Position - transformHeaderSize;
		set => ChunkFileStream.Position = value + transformHeaderSize;
	}

	public override void SetLength(long value) => ChunkFileStream.SetLength(value + transformHeaderSize);
	public override long Length => ChunkFileStream.Length + transformHeaderSize;
}
