// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive.Naming;

namespace EventStore.Core.Services.Archive.Storage;

public interface IArchiveStorageReader {
	public IArchiveChunkNameResolver ChunkNameResolver { get; } //qq need?

	/// <returns>A position in the transaction log up to which all chunks have been archived</returns>
	public ValueTask<long> GetCheckpoint(CancellationToken ct);

	ValueTask<int> ReadAsync(int logicalChunkNumber, Memory<byte> buffer, int offset, CancellationToken ct);
}
