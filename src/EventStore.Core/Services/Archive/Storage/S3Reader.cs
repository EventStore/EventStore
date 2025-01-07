// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using EventStore.Common.Exceptions;
using EventStore.Core.Services.Archive.Naming;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using FluentStorage;
using FluentStorage.AWS.Blobs;
using FluentStorage.Blobs;
using Serilog;

namespace EventStore.Core.Services.Archive.Storage;

public class S3Reader : FluentReader, IArchiveStorageReader {
	private readonly S3Options _options;
	private readonly IAwsS3BlobStorage _awsBlobStorage;

	public S3Reader(
		S3Options options,
		IArchiveChunkNameResolver chunkNameResolver,
		string archiveCheckpointFile)
		: base(chunkNameResolver, archiveCheckpointFile) {

		_options = options;

		if (string.IsNullOrEmpty(options.Bucket))
			throw new InvalidConfigurationException("Please specify an Archive S3 Bucket");

		if (string.IsNullOrEmpty(options.Region))
			throw new InvalidConfigurationException("Please specify an Archive S3 Region");

		_awsBlobStorage = StorageFactory.Blobs.AwsS3(
			awsCliProfileName: options.AwsCliProfileName,
			bucketName: options.Bucket,
			region: options.Region) as IAwsS3BlobStorage;
	}

	protected override ILogger Log { get; } = Serilog.Log.ForContext<S3Reader>();

	protected override IBlobStorage BlobStorage => _awsBlobStorage;

	public async ValueTask<Stream> GetChunk(int logicalChunkNumber, long start, long end, CancellationToken ct) {
		var chunkFile = await ChunkNameResolver.GetFileNameFor(logicalChunkNumber);
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

	public override async IAsyncEnumerable<string> ListChunks([EnumeratorCancellation] CancellationToken ct) {
		var listResponse = _awsBlobStorage.NativeBlobClient.Paginators.ListObjectsV2(new ListObjectsV2Request {
			BucketName = _options.Bucket,
			Prefix = ChunkNameResolver.Prefix,
		});

		await foreach (var s3Object in listResponse.S3Objects.WithCancellation(ct))
			yield return s3Object.Key;
	}
}
