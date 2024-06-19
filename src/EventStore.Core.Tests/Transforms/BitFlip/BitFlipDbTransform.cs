using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.BitFlip;

public class BitFlipDbTransform : IDbTransform {
	public string Name => "bitflip";
	public TransformType Type => (TransformType) 0xFF;
	public IChunkTransformFactory ChunkFactory { get; } = new BitFlipChunkTransformFactory();
}
