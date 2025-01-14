// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Plugins.Transforms;

namespace EventStore.Security.EncryptionAtRest.Transforms.AesGcm;

public sealed class AesGcmChunkWriteTransform(ReadOnlyMemory<byte> key, int transformHeaderSize, IChunkReadTransform readTransform) : IChunkWriteTransform {
	private AesGcmChunkWriteStream _transformedStream;

	public ChunkDataWriteStream TransformData(ChunkDataWriteStream dataStream) {
		ArgumentNullException.ThrowIfNull(dataStream);

		_transformedStream = new AesGcmChunkWriteStream(dataStream, key.Span, transformHeaderSize, readTransform);
		return _transformedStream;
	}

	public void CompleteData(int footerSize, int alignmentSize) {
		_transformedStream.WriteLastBlock(commit: true);

		var chunkHeaderAndDataSize = (int)_transformedStream.ChunkFileStream.Position;
		var alignedSize = GetAlignedSize(chunkHeaderAndDataSize + footerSize, alignmentSize);
		var paddingSize = alignedSize - chunkHeaderAndDataSize - footerSize;
		if (paddingSize > 0) {
			var padding = new byte[paddingSize];
			_transformedStream.ChunkFileStream.Write(padding);
			_transformedStream.ChecksumAlgorithm.TransformBlock(padding, 0, padding.Length, null, 0);
		}
	}

	public void WriteFooter(ReadOnlySpan<byte> footer, out int fileSize) {
		_transformedStream.ChunkFileStream.Write(footer);
		fileSize = (int)_transformedStream.ChunkFileStream.Length;
	}

	private static int GetAlignedSize(int size, int alignmentSize) {
		if (size % alignmentSize == 0) return size;
		return (size / alignmentSize + 1) * alignmentSize;
	}
}
