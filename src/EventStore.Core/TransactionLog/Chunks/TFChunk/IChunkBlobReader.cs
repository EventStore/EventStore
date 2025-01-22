// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using DotNext.IO.Pipelines;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk;

public interface IChunkBlobReader : IDisposable {
	public void SetPosition(long position);
	public ValueTask<BulkReadResult> ReadNextBytes(Memory<byte> buffer, CancellationToken token);
}

public static class ChunkBlobReader {
	public static async ValueTask CopyToAsync(this IChunkBlobReader reader, PipeWriter writer, CancellationToken token) {
		BulkReadResult readResult;
		FlushResult flushResult;
		do {
			var buffer = writer.GetMemory();
			readResult = await reader.ReadNextBytes(buffer, token);
			writer.Advance(readResult.BytesRead);
			flushResult = await writer.FlushAsync(token);
			flushResult.ThrowIfCancellationRequested(token);
		} while (!readResult.IsEOF && !flushResult.IsCompleted);
	}
}
