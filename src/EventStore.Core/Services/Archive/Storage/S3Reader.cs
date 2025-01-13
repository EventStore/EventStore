// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DotNext.Buffers;
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
		var chunkFile = await ChunkNameResolver.ResolveFileName(logicalChunkNumber, ct);
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

	public async ValueTask<int> ReadAsync(int logicalChunkNumber, Memory<byte> buffer, long offset, CancellationToken ct) {
		var request = new GetObjectRequest {
			BucketName = _awsBlobStorage.BucketName,
			Key = await ChunkNameResolver.ResolveFileName(logicalChunkNumber, ct),
			ByteRange = GetRange(offset, buffer.Length),
		};

		using var response = await _awsBlobStorage.NativeBlobClient.GetObjectAsync(request, ct);
		var length = int.CreateSaturating(response.ContentLength);
		await using var responseStream = response.ResponseStream;
		await responseStream.ReadExactlyAsync(buffer.TrimLength(length), ct);
		return length;
	}

	private static ByteRange GetRange(long offset, int length) => new(offset, offset + length - 1L);

	public async ValueTask<ArchivedChunkMetadata> GetMetadataAsync(int logicalChunkNumber, CancellationToken ct) {
		var objectName = await ChunkNameResolver.ResolveFileName(logicalChunkNumber, ct);
		var response =
			await _awsBlobStorage.NativeBlobClient.GetObjectMetadataAsync(_awsBlobStorage.BucketName, objectName, ct);
		return new(Size: response.ContentLength);
	}
}
