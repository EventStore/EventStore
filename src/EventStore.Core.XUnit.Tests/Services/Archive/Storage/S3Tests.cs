// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

//#define RUN_S3_TESTS // uncomment to run S3 tests, requires AWS CLI to be installed and configured

using EventStore.Core.Services.Archive;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Storage;

#if RUN_S3_TESTS
public class S3ReaderTests : ArchiveStorageReaderTests<S3ReaderTests> {
	protected override StorageType StorageType => StorageType.S3;
}

public class S3WriterTests : ArchiveStorageWriterTests<S3WriterTests> {
	protected override StorageType StorageType => StorageType.S3;
}
#endif
