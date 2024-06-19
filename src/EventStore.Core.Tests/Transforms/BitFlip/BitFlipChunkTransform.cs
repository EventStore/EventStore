using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.BitFlip;

public class BitFlipChunkTransform : IChunkTransform {
	public IChunkReadTransform Read { get; } = new BitFlipChunkReadTransform();
	public IChunkWriteTransform Write { get; } = new BitFlipChunkWriteTransform();
}
