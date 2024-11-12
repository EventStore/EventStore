// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace EventStore.Core.Services.Archiver.Storage;

public class ArchiveStorageFactory(
	ArchiverOptions options,
	IVersionedFileNamingStrategy fileNamingStrategy) : IArchiveStorageFactory {

	public IArchiveStorage Create() {
		return options.StorageType switch {
			StorageType.FileSystem => new FileSystemArchiveStorage(options.FileSystem, fileNamingStrategy.GetPrefixFor),
			StorageType.S3 => new S3ArchiveStorage(options.S3, fileNamingStrategy.GetPrefixFor),
			_ => throw new ArgumentOutOfRangeException(nameof(options.StorageType))
		};
	}
}
