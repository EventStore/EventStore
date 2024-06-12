using System;

namespace EventStore.Core.Transforms;

public interface IChunkWriteTransform {
	ChunkDataWriteStream TransformData(ChunkDataWriteStream stream);
	void CompleteData(int footerSize, int alignmentSize);
	void WriteFooter(ReadOnlySpan<byte> footer, out int fileSize);

	public static IChunkWriteTransform CreateIdentityTransform() => new IdentityChunkWriteTransform();
}

file sealed class IdentityChunkWriteTransform : IChunkWriteTransform {
	private ChunkDataWriteStream _stream;

	public ChunkDataWriteStream TransformData(ChunkDataWriteStream stream) {
		_stream = stream;
		return _stream;
	}

	public void CompleteData(int footerSize, int alignmentSize) {
		var chunkHeaderAndDataSize = (int)_stream.Position;
		var alignedSize = GetAlignedSize(chunkHeaderAndDataSize + footerSize, alignmentSize);
		var paddingSize = alignedSize - chunkHeaderAndDataSize - footerSize;
		if (paddingSize > 0)
			_stream.Write(new byte[paddingSize]);
	}

	public void WriteFooter(ReadOnlySpan<byte> footer, out int fileSize) {
		_stream.ChunkFileStream.Write(footer);
		fileSize = (int)_stream.Length;
	}

	private static int GetAlignedSize(int size, int alignmentSize) {
		if (size % alignmentSize == 0) return size;
		return (size / alignmentSize + 1) * alignmentSize;
	}
}
