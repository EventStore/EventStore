using System;
using System.IO;
using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.ByteDup;
public class ByteDupChunkTransformFactory : IChunkTransformFactory {
	public TransformType Type => (TransformType) 0xFE;
	public int TransformDataPosition(int dataPosition) => dataPosition * 2;
	public ReadOnlyMemory<byte> CreateTransformHeader() => ReadOnlyMemory<byte>.Empty;
	public ReadOnlyMemory<byte> ReadTransformHeader(Stream stream) => ReadOnlyMemory<byte>.Empty;
	public IChunkTransform CreateTransform(ReadOnlyMemory<byte> transformHeader) => new ByteDupChunkTransform();
}
