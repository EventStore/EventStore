// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DotNext.Buffers;
using DotNext.IO;
using EventStore.Core.TransactionLog.Chunks.TFChunk;

namespace EventStore.Core.Services.Archive.Storage.S3;

public sealed class S3ChunkHandle : IChunkHandle {
	private readonly IAmazonS3 _client;
	private readonly string _bucketName;
	private readonly string _key;
	private readonly long _length;

	private S3ChunkHandle(IAmazonS3 client, string bucketName, string key, FileAccess access, long length) {
		Debug.Assert(client is not null);
		Debug.Assert(bucketName is { Length: > 0 });
		Debug.Assert(key is { Length: > 0 });
		Debug.Assert(length >= 0L);
		Debug.Assert(Enum.IsDefined(access));

		_client = client;
		_bucketName = bucketName;
		_key = key;
		_length = length;
		Access = access;
	}

	public static async ValueTask<IChunkHandle> OpenForReadAsync(IAmazonS3 client, string bucketName, string key,
		CancellationToken token) {
		var response = await client.GetObjectMetadataAsync(bucketName, key, token);
		return new S3ChunkHandle(client, bucketName, key, FileAccess.Read, response.ContentLength);
	}

	void IFlushable.Flush() {
		// nothing to flush
	}

	Task IFlushable.FlushAsync(CancellationToken token) => CompletedOrCanceled(token);

	void IDisposable.Dispose() {
		// nothing to dispose
	}

	public ValueTask WriteAsync(ReadOnlyMemory<byte> data, long offset, CancellationToken token)
		=> ValueTask.FromException(new NotSupportedException());

	public async ValueTask<int> ReadAsync(Memory<byte> buffer, long offset, CancellationToken token) {
		var request = new GetObjectRequest {
			BucketName = _bucketName,
			Key = _key,
			ByteRange = GetRange(offset, buffer.Length),
		};

		using var response = await _client.GetObjectAsync(request, token);
		var length = int.CreateSaturating(response.ContentLength);
		await using var responseStream = response.ResponseStream;
		await responseStream.ReadExactlyAsync(buffer.TrimLength(length), token);
		return length;
	}

	private static ByteRange GetRange(long offset, int length) => new(offset, offset + length - 1L);

	public long Length {
		get => _length;
		set => throw new NotSupportedException();
	}

	public FileAccess Access { get; }

	public ValueTask SetReadOnlyAsync(bool value, CancellationToken token)
		=> new(CompletedOrCanceled(token));

	private static Task CompletedOrCanceled(CancellationToken token)
		=> token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;
}
