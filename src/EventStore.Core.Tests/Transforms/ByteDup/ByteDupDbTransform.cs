using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.ByteDup;
public class ByteDupDbTransform : IDbTransform {
	public string Name => "bytedup";
	public TransformType Type => (TransformType) 0xFE;
	public IChunkTransformFactory ChunkFactory { get; } = new ByteDupChunkTransformFactory();
}
