// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive.Storage;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk;

public static class ArchiveStorageReaderExtensions {
	public static ValueTask<IChunkHandle> OpenForReadAsync(this IArchiveStorageReader self, int chunkNumber, CancellationToken token) =>
		ArchivedChunkHandle.OpenForReadAsync(self, chunkNumber, token);
}
