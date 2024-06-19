using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.WithHeader;
public class WithHeaderDbTransform : IDbTransform {
	public string Name => "withheader";
	public TransformType Type => (TransformType) 0xFD;
	public IChunkTransformFactory ChunkFactory { get; } = new WithHeaderChunkTransformFactory();
}
