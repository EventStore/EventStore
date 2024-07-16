using System;
using System.IO;
using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.BitFlip;

public class BitFlipChunkTransformFactory : IChunkTransformFactory {
	public TransformType Type => (TransformType) 0xFF;
	public int TransformDataPosition(int dataPosition) => dataPosition;
	public ReadOnlyMemory<byte> CreateTransformHeader() => ReadOnlyMemory<byte>.Empty;
	public ReadOnlyMemory<byte> ReadTransformHeader(Stream stream) => ReadOnlyMemory<byte>.Empty;
	public IChunkTransform CreateTransform(ReadOnlyMemory<byte> transformHeader) => new BitFlipChunkTransform();
}
