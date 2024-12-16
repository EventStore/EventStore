// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using FluentStorage;
using FluentStorage.AWS.Blobs;
using FluentStorage.Blobs;
using Serilog;

namespace EventStore.Core.Services.Archive.Storage;

public class S3Reader : FluentReader, IArchiveStorageReader {
	private readonly S3Options _options;
	private readonly IAwsS3BlobStorage _awsBlobStorage;

	public S3Reader(S3Options options, Func<int?, int?, string> getChunkPrefix, string archiveCheckpointFile)
		: base(archiveCheckpointFile) {
		_options = options;
		_awsBlobStorage = StorageFactory.Blobs.AwsS3(
			awsCliProfileName: options.AwsCliProfileName,
			bucketName: options.Bucket,
			region: options.Region) as IAwsS3BlobStorage;
	}

	protected override ILogger Log { get; } = Serilog.Log.ForContext<S3Reader>();

	protected override IBlobStorage BlobStorage => _awsBlobStorage;

	public async ValueTask<Stream> GetChunk(string chunkFile, long start, long end, CancellationToken ct) {
		var request = new GetObjectRequest {
			BucketName = _options.Bucket,
			Key = chunkFile,
			ByteRange = new ByteRange(start, end),
		};

		try {
			var client = _awsBlobStorage.NativeBlobClient;
			var response = await client.GetObjectAsync(request, ct);
			return response.ResponseStream;
		} catch (AmazonS3Exception ex) {
			if (ex.ErrorCode == "NoSuchKey")
				throw new ChunkDeletedException();
			throw;
		}
	}
}
