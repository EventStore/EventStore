// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Services.Archive.Naming;

namespace EventStore.Core.Services.Archive.Storage;

public class ArchiveStorageFactory(
	ArchiveOptions options,
	IArchiveChunkNamer chunkNamer) : IArchiveStorageFactory {

	private const string ArchiveCheckpointFile = "archive.chk";

	public IArchiveStorageReader CreateReader() {
		return options.StorageType switch {
			StorageType.Unspecified => NoArchiveReader.Instance,
			StorageType.FileSystem => new FileSystemReader(options.FileSystem, chunkNamer, ArchiveCheckpointFile),
			StorageType.S3 => new S3Reader(options.S3, chunkNamer, ArchiveCheckpointFile),
			_ => throw new ArgumentOutOfRangeException(nameof(options.StorageType))
		};
	}

	public IArchiveStorageWriter CreateWriter() {
		// NB: StorageFactory.Blobs.DirectoryFiles does not appear to offer atomic file 'upload' so
		// we use our own implementation instead (todo: consider if it could be an IBlobStorage)
		return options.StorageType switch {
			StorageType.Unspecified => throw new InvalidOperationException("Please specify an Archive StorageType"),
			StorageType.FileSystem => new FileSystemWriter(options.FileSystem, ArchiveCheckpointFile),
			StorageType.S3 => new S3Writer(options.S3, ArchiveCheckpointFile),
			_ => throw new ArgumentOutOfRangeException(nameof(options.StorageType))
		};
	}
}
