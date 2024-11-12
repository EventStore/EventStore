// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Services.Archiver;

public class ArchiverOptions {
	public bool Enabled { get; init; } = true;
	public StorageType StorageType { get; init; } = StorageType.None;
	public FileSystemOptions FileSystem { get; init; } = new();
	public S3Options S3 { get; init; } = new();
}

public enum StorageType {
	None,
	FileSystem,
	S3,
}

public class FileSystemOptions {
	public string Path { get; init; } = "";
}

public class S3Options {
	public string Bucket { get; init; } = "";
}
