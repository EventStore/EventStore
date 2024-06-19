using System;
using System.IO;
using System.Security.Cryptography;
using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.WithHeader;
public class WithHeaderChunkTransformFactory : IChunkTransformFactory {
	private const int TransformHeaderSize = 133;
	private readonly byte[] _header;

	public TransformType Type => (TransformType) 0xFD;
	public int TransformDataPosition(int dataPosition) => TransformHeaderSize + dataPosition;

	public WithHeaderChunkTransformFactory() {
		_header = new byte[TransformHeaderSize];
		RandomNumberGenerator.Fill(_header);
	}

	public ReadOnlyMemory<byte> CreateTransformHeader() => _header;

	public ReadOnlyMemory<byte> ReadTransformHeader(Stream stream) {
		var buffer = new byte[TransformHeaderSize];
		stream.ReadExactly(buffer);
		return buffer.AsMemory();
	}

	public IChunkTransform CreateTransform(ReadOnlyMemory<byte> transformHeader) =>
		new WithHeaderChunkTransform(transformHeader.Length);
}
