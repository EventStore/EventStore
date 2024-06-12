using DotNext.Patterns;

namespace EventStore.Core.Transforms;

public interface IDbTransform {
	string Name { get; }
	TransformType Type { get; }
	IChunkTransformFactory ChunkFactory { get; }

	public static IDbTransform Identity => IdentityDbTransform.Instance;
}

file sealed class IdentityDbTransform : IDbTransform, ISingleton<IdentityDbTransform> {
	public static IdentityDbTransform Instance { get; } = new();

	private IdentityDbTransform() {

	}

	public string Name => "identity";
	public TransformType Type => TransformType.Identity;
	public IChunkTransformFactory ChunkFactory => IChunkTransformFactory.Identity;
}
