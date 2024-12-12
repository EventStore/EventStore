// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.WithHeader;
public class WithHeaderChunkWriteTransform(int transformHeaderSize) : IChunkWriteTransform {
	private ChunkDataWriteStream _transformedStream;

	public ChunkDataWriteStream TransformData(ChunkDataWriteStream stream) {
		_transformedStream = new WithHeaderChunkWriteStream(stream, transformHeaderSize);
		return _transformedStream;
	}

	public async ValueTask CompleteData(int footerSize, int alignmentSize, CancellationToken token) {
		var chunkHeaderAndDataSize = (int)_transformedStream.ChunkFileStream.Position;
		var alignedSize = GetAlignedSize(chunkHeaderAndDataSize + footerSize, alignmentSize);
		var paddingSize = alignedSize - chunkHeaderAndDataSize - footerSize;
		if (paddingSize > 0) {
			var padding = new byte[paddingSize];
			await _transformedStream.ChunkFileStream.WriteAsync(padding, token);
			_transformedStream.ChecksumAlgorithm.AppendData(padding);
		}
	}

	public async ValueTask<int> WriteFooter(ReadOnlyMemory<byte> footer, CancellationToken token) {
		await _transformedStream.ChunkFileStream.WriteAsync(footer, token);
		return (int)_transformedStream.ChunkFileStream.Length;
	}

	private static int GetAlignedSize(int size, int alignmentSize) {
		if (size % alignmentSize == 0) return size;
		return (size / alignmentSize + 1) * alignmentSize;
	}
}
