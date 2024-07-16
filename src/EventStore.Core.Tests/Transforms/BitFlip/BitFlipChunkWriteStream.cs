using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.BitFlip;

public class BitFlipChunkWriteStream(ChunkDataWriteStream stream) :
	ChunkDataWriteStream(stream.ChunkFileStream, stream.ChecksumAlgorithm) {
	public override void Write(byte[] buffer, int offset, int count) {
		var buf = new byte[count];
		for (int i = 0; i < count; i++)
			buf[i] = (byte)(buffer[i + offset] ^ 0xFF);
		ChunkFileStream.Write(buf, 0, buf.Length);
		ChecksumAlgorithm.TransformBlock(buf, 0, buf.Length, null, 0);
	}
}
