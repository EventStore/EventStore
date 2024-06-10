namespace EventStore.Core.Transforms;

public interface IChunkReadTransform {
	ChunkDataReadStream TransformData(ChunkDataReadStream stream);
}
