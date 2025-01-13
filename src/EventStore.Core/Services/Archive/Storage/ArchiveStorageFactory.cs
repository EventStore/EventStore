// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Services.Archive.Naming;

namespace EventStore.Core.Services.Archive.Storage;

//qq convert to static factory method
public class ArchiveStorageFactory(
	ArchiveOptions options,
	IArchiveChunkNameResolver chunkNameResolver) : IArchiveStorageFactory {

	private const string ArchiveCheckpointFile = "archive.chk";

	public IArchiveStorageReader CreateReader() {
		return options.StorageType switch {
			StorageType.Unspecified => NoArchiveReader.Instance,
			StorageType.FileSystem => new ArchiveStorageReader(new FileSystemBlobStorage(options.FileSystem), chunkNameResolver, ArchiveCheckpointFile),
			StorageType.S3 => new ArchiveStorageReader(new S3BlobStorage(options.S3), chunkNameResolver, ArchiveCheckpointFile),
			_ => throw new ArgumentOutOfRangeException(nameof(options.StorageType))
		};
	}

	public IArchiveStorageWriter CreateWriter() {
		// NB: StorageFactory.Blobs.DirectoryFiles does not appear to offer atomic file 'upload' so
		// we use our own implementation instead (todo: consider if it could be an IBlobStorage)
		return options.StorageType switch {
			StorageType.Unspecified => throw new InvalidOperationException("Please specify an Archive StorageType"),
			StorageType.FileSystem => new ArchiveStorageWriter(new FileSystemBlobStorage(options.FileSystem), chunkNameResolver, ArchiveCheckpointFile),
			StorageType.S3 => new ArchiveStorageWriter(new S3BlobStorage(options.S3), chunkNameResolver, ArchiveCheckpointFile),
			_ => throw new ArgumentOutOfRangeException(nameof(options.StorageType))
		};
	}
}
