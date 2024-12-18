// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Services.Archive;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Storage;

public class FileSystemReaderTests : ArchiveStorageReaderTests<FileSystemReaderTests> {
	protected override StorageType StorageType => StorageType.FileSystem;
}

public class FileSystemWriterTests : ArchiveStorageWriterTests<FileSystemWriterTests> {
	protected override StorageType StorageType => StorageType.FileSystem;
}
