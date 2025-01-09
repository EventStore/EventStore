// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace EventStore.Core.Services.Archive.Naming;

public class ArchiveChunkNamer : IArchiveChunkNamer {
	private readonly IVersionedFileNamingStrategy _namingStrategy;

	public ArchiveChunkNamer(IVersionedFileNamingStrategy namingStrategy) {
		_namingStrategy = namingStrategy;
	}

	public string Prefix => _namingStrategy.Prefix;

	public string GetFileNameFor(int logicalChunkNumber) {
		ArgumentOutOfRangeException.ThrowIfNegative(logicalChunkNumber);

		var filePath = _namingStrategy.GetFilenameFor(logicalChunkNumber, version: 1);
		return Path.GetFileName(filePath);
	}
}
