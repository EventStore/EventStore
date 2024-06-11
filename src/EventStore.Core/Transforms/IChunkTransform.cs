namespace EventStore.Core.Transforms;

public interface IChunkTransform {
	IChunkReadTransform Read { get; }
	IChunkWriteTransform Write { get; }

	public static IChunkTransform CreateIdentityTransform() => new IdentityChunkTransform();
}

file sealed class IdentityChunkTransform : IChunkTransform {
	public IChunkReadTransform Read => IChunkReadTransform.Identity;
	public IChunkWriteTransform Write { get; } = IChunkWriteTransform.CreateIdentityTransform();
}
