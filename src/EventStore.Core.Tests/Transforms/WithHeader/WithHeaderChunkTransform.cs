using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.WithHeader;
public class WithHeaderChunkTransform(int transformHeaderSize) : IChunkTransform {
	public IChunkReadTransform Read { get; } = new WithHeaderChunkReadTransform(transformHeaderSize);
	public IChunkWriteTransform Write { get; } = new WithHeaderChunkWriteTransform(transformHeaderSize);
}
