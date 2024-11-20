// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using FluentStorage;

namespace EventStore.Core.Services.Archive.Storage;

public class S3Writer : FluentWriter {
	public S3Writer(S3Options options, Func<int?, int?, string> getChunkPrefix)
		: base(StorageFactory.Blobs.AwsS3(
			awsCliProfileName: options.AwsCliProfileName,
			bucketName: options.Bucket,
			region: options.Region)) {
	}
}
