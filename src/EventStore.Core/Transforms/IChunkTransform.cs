namespace EventStore.Core.Transforms;

public interface IChunkTransform {
	IChunkReadTransform Read { get; }
	IChunkWriteTransform Write { get; }
}
