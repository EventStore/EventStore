// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.Services.Archive.Storage;

public interface IArchiveStorageReader {
	/// <returns>A position in the transaction log up to which all chunks have been archived</returns>
	public ValueTask<long> GetCheckpoint(CancellationToken ct);

	ValueTask<int> ReadAsync(int logicalChunkNumber, Memory<byte> buffer, long offset, CancellationToken ct);

	ValueTask<ArchivedChunkMetadata> GetMetadataAsync(int logicalChunkNumber, CancellationToken token);
}

public readonly record struct ArchivedChunkMetadata(long PhysicalSize);
