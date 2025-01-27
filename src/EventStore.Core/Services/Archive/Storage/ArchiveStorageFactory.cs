// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Services.Archive.Naming;
using EventStore.Core.Services.Archive.Storage.S3;

namespace EventStore.Core.Services.Archive.Storage;

public static class ArchiveStorageFactory {
	private const string ArchiveCheckpointFile = "archive.chk";

	public static IArchiveStorage Create(ArchiveOptions options, IArchiveNamingStrategy namingStrategy) =>
		options.StorageType switch {
			StorageType.Unspecified => throw new InvalidOperationException("Please specify an Archive StorageType"),
			StorageType.FileSystem => new ArchiveStorage(new FileSystemBlobStorage(options.FileSystem), namingStrategy, ArchiveCheckpointFile),
			StorageType.S3 => new ArchiveStorage(new S3BlobStorage(options.S3), namingStrategy, ArchiveCheckpointFile),
			_ => throw new ArgumentOutOfRangeException(nameof(options.StorageType))
		};
}
