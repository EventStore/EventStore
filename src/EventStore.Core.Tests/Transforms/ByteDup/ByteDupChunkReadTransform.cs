using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.ByteDup;
public class ByteDupChunkReadTransform : IChunkReadTransform {
	public ChunkDataReadStream TransformData(ChunkDataReadStream dataStream) =>
		new ByteDupChunkReadStream(dataStream);
}
