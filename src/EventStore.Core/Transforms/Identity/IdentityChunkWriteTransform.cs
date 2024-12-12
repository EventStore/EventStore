// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Buffers;
using EventStore.Plugins.Transforms;

namespace EventStore.Core.Transforms.Identity;

public sealed class IdentityChunkWriteTransform : IChunkWriteTransform {
	private ChunkDataWriteStream _stream;

	public ChunkDataWriteStream TransformData(ChunkDataWriteStream stream) {
		_stream = stream;
		return _stream;
	}

	public ValueTask CompleteData(int footerSize, int alignmentSize, CancellationToken token) {
		var chunkHeaderAndDataSize = (int)_stream.Position;
		var alignedSize = GetAlignedSize(chunkHeaderAndDataSize + footerSize, alignmentSize);
		var paddingSize = alignedSize - chunkHeaderAndDataSize - footerSize;
		return paddingSize > 0
			? WritePaddingAsync(_stream, paddingSize, token)
			: ValueTask.CompletedTask;

		static async ValueTask WritePaddingAsync(ChunkDataWriteStream stream, int paddingSize, CancellationToken token) {
			using var buffer = Memory.AllocateExactly<byte>(paddingSize);
			buffer.Span.Clear(); // ensure that the padding is zeroed
			await stream.WriteAsync(buffer.Memory, token);
		}
	}

	public async ValueTask<int> WriteFooter(ReadOnlyMemory<byte> footer, CancellationToken token) {
		await _stream.ChunkFileStream.WriteAsync(footer, token);
		return (int)_stream.Length;
	}

	private static int GetAlignedSize(int size, int alignmentSize) {
		var quotient = Math.DivRem(size, alignmentSize, out var remainder);

		return remainder is 0 ? size : (quotient + 1) * alignmentSize;
	}
}
