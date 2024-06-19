using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.WithHeader;
public class WithHeaderChunkReadTransform(int transformHeaderSize) : IChunkReadTransform {
	public ChunkDataReadStream TransformData(ChunkDataReadStream dataStream) =>
		new WithHeaderChunkReadStream(dataStream, transformHeaderSize);
}
