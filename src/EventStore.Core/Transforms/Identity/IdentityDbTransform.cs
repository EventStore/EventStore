using EventStore.Plugins.Transforms;

namespace EventStore.Core.Transforms.Identity;

public sealed class IdentityDbTransform : IDbTransform {
	public string Name => "identity";
	public TransformType Type => TransformType.Identity;
	public IChunkTransformFactory ChunkFactory { get; } = new IdentityChunkTransformFactory();
}
