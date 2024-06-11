using DotNext.Patterns;

namespace EventStore.Core.Transforms;

public interface IChunkReadTransform {
	ChunkDataReadStream TransformData(ChunkDataReadStream stream);

	public static IChunkReadTransform Identity => IdentityChunkReadTransform.Instance;
}

file sealed class IdentityChunkReadTransform : IChunkReadTransform, ISingleton<IdentityChunkReadTransform> {
	public static IdentityChunkReadTransform Instance { get; } = new();

	private IdentityChunkReadTransform() {
	}

	public ChunkDataReadStream TransformData(ChunkDataReadStream stream) => stream;
}
