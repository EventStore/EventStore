// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace EventStore.Core.Services.Archive.Storage;

public class S3ArchiveStorage : IArchiveStorage {
	protected static readonly ILogger Log = Serilog.Log.ForContext<S3ArchiveStorage>();
	private readonly string _bucket;
	private readonly Func<int?, int?, string> _getChunkPrefix;

	public S3ArchiveStorage(S3Options options, Func<int?, int?, string> getChunkPrefix) {
		_bucket = options.Bucket;
		_getChunkPrefix = getChunkPrefix;
	}
	public ValueTask<bool> StoreChunk(string chunkPath, CancellationToken ct) {
		throw new NotImplementedException();
	}

	public ValueTask<bool> RemoveChunks(int chunkStartNumber, int chunkEndNumber, string exceptChunk, CancellationToken ct) {
		throw new NotImplementedException();
	}

	public IAsyncEnumerable<string> ListChunks(CancellationToken ct) {
		throw new NotImplementedException();
	}
}
