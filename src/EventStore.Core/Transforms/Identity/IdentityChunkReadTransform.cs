using EventStore.Plugins.Transforms;

namespace EventStore.Core.Transforms.Identity;

public sealed class IdentityChunkReadTransform : IChunkReadTransform {
	public static readonly IdentityChunkReadTransform Instance = new();

	private IdentityChunkReadTransform() {

	}

	public ChunkDataReadStream TransformData(ChunkDataReadStream stream) => stream;
}
