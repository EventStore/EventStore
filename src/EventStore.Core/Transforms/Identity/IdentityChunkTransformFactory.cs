using System;
using System.IO;

namespace EventStore.Core.Transforms.Identity;

public sealed class IdentityChunkTransformFactory : IChunkTransformFactory {
	public TransformType Type => TransformType.Identity;
	public int TransformDataPosition(int dataPosition) => dataPosition;
	public ReadOnlyMemory<byte> CreateTransformHeader() => ReadOnlyMemory<byte>.Empty;
	public ReadOnlyMemory<byte> ReadTransformHeader(Stream stream) => ReadOnlyMemory<byte>.Empty;
	public IChunkTransform CreateTransform(ReadOnlyMemory<byte> transformHeader) => new IdentityChunkTransform();
}
