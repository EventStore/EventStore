using System;
using System.IO;
using DotNext.Patterns;

namespace EventStore.Core.Transforms;

public interface IChunkTransformFactory {
	TransformType Type { get; }
	int TransformDataPosition(int dataPosition);
	ReadOnlyMemory<byte> CreateTransformHeader();
	ReadOnlyMemory<byte> ReadTransformHeader(Stream stream);
	IChunkTransform CreateTransform(ReadOnlyMemory<byte> transformHeader);

	public static IChunkTransformFactory Identity => IdentityChunkTransformFactory.Instance;
}

file sealed class IdentityChunkTransformFactory : IChunkTransformFactory, ISingleton<IdentityChunkTransformFactory> {
	public static IdentityChunkTransformFactory Instance { get; } = new();

	private IdentityChunkTransformFactory() {

	}

	public TransformType Type => TransformType.Identity;
	public int TransformDataPosition(int dataPosition) => dataPosition;
	public ReadOnlyMemory<byte> CreateTransformHeader() => ReadOnlyMemory<byte>.Empty;
	public ReadOnlyMemory<byte> ReadTransformHeader(Stream stream) => ReadOnlyMemory<byte>.Empty;
	public IChunkTransform CreateTransform(ReadOnlyMemory<byte> transformHeader) => IChunkTransform.CreateIdentityTransform();
}
