using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.BitFlip;

public class BitFlipChunkReadTransform : IChunkReadTransform {
	public ChunkDataReadStream TransformData(ChunkDataReadStream dataStream) =>
		new BitFlipChunkReadStream(dataStream);
}
