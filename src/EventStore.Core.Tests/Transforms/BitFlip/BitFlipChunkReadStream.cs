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
