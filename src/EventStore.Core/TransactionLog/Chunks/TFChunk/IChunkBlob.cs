// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk;

/// <summary>
/// Interprets database chunk as a blob.
/// </summary>
public interface IChunkBlob : IDisposable {
	ValueTask<IChunkBlobReader> AcquireRawReader(CancellationToken token);

	IAsyncEnumerable<IChunkBlob> UnmergeAsync();

	ChunkHeader ChunkHeader { get; }
}
