namespace EventStore.Core.Transforms;

public interface IDbTransform {
	string Name { get; }
	TransformType Type { get; }
	IChunkTransformFactory ChunkFactory { get; }
}
