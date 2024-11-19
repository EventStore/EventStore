// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Threading;
using Serilog;

namespace EventStore.Core.Services.Archive.Storage;

public class S3Reader : IArchiveStorageReader {
	protected static readonly ILogger Log = Serilog.Log.ForContext<S3Reader>();
	private readonly string _bucket;
	private readonly Func<int?, int?, string> _getChunkPrefix;

	public S3Reader(S3Options options, Func<int?, int?, string> getChunkPrefix) {
		_bucket = options.Bucket;
		_getChunkPrefix = getChunkPrefix;
	}

	public IAsyncEnumerable<string> ListChunks(CancellationToken ct) {
		throw new NotImplementedException();
	}
}
