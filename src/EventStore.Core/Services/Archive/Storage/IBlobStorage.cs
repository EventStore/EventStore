// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.Services.Archive.Storage;

// The purpose of this is to make it easy to implement additional cloud providers for archiving.
// If the cloud provider is not suitable for this interface then implement IArchiveStorage instead.
public interface IBlobStorage {
	ValueTask<int> ReadAsync(string name, Memory<byte> buffer, long offset, CancellationToken token);
	ValueTask<BlobMetadata> GetMetadataAsync(string name, CancellationToken token);
	ValueTask StoreAsync(Stream readableStream, string name, CancellationToken ct);
}

public readonly record struct BlobMetadata(long Size);
