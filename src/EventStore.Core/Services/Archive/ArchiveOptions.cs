// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Services.Archive;

public class ArchiveOptions {
	public bool Enabled { get; init; } = false;
	public StorageType StorageType { get; init; } = StorageType.Unspecified;
	public FileSystemOptions FileSystem { get; init; } = new();
	public S3Options S3 { get; init; } = new();
	public RetentionOptions RetainAtLeast { get; init; } = new();
}

public enum StorageType {
	Unspecified,
	FileSystem,
	S3,
}

public class FileSystemOptions {
	public string Path { get; init; } = "";
}

public class S3Options {
	public string AwsCliProfileName { get; init; } = "default";
	public string Bucket { get; init; } = "";
	public string Region { get; init; } = "";
}

public class RetentionOptions {
	public long Days { get; init; }
	// number of bytes in the logical log
	public long LogicalBytes { get; init; }
}
