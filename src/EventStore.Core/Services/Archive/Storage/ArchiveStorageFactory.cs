// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace EventStore.Core.Services.Archive.Storage;

public class ArchiveStorageFactory(
	ArchiveOptions options,
	IVersionedFileNamingStrategy fileNamingStrategy) : IArchiveStorageFactory {

	public IArchiveStorageReader CreateReader() {
		return options.StorageType switch {
			StorageType.FileSystem => new FileSystemReader(options.FileSystem, fileNamingStrategy.GetPrefixFor),
			StorageType.S3 => new S3Reader(options.S3, fileNamingStrategy.GetPrefixFor),
			_ => throw new ArgumentOutOfRangeException(nameof(options.StorageType))
		};
	}

	public IArchiveStorageWriter CreateWriter() {
		return options.StorageType switch {
			StorageType.FileSystem => new FileSystemWriter(options.FileSystem, fileNamingStrategy.GetPrefixFor),
			StorageType.S3 => new S3Writer(options.S3, fileNamingStrategy.GetPrefixFor),
			_ => throw new ArgumentOutOfRangeException(nameof(options.StorageType))
		};
	}
}
