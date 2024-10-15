// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading.Tasks;
using Serilog;

namespace EventStore.Core.Services.Archiver.Storage;

public class S3ArchiveStorage : IArchiveStorage {
	protected static readonly ILogger Log = Serilog.Log.ForContext<S3ArchiveStorage>();
	private readonly string _bucket;

	public S3ArchiveStorage(S3Options options) {
		_bucket = options.Bucket;
	}

	public ValueTask StoreChunk(string pathToSourceChunk) {
		Log.Information("Pretending to upload \"{Path}\" to S3 bucket \"{Bucket}\"", pathToSourceChunk, _bucket);
		return ValueTask.CompletedTask;
	}
}
