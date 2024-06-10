namespace EventStore.Core.Transforms.Identity;

public sealed class IdentityChunkTransform : IChunkTransform {
	public IChunkReadTransform Read => IdentityChunkReadTransform.Instance;
	public IChunkWriteTransform Write { get; } = new IdentityChunkWriteTransform();
}
