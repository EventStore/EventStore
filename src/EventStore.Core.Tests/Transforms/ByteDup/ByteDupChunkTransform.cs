using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.ByteDup;
public class ByteDupChunkTransform : IChunkTransform {
	public IChunkReadTransform Read { get; } = new ByteDupChunkReadTransform();
	public IChunkWriteTransform Write { get; } = new ByteDupChunkWriteTransform();
}
