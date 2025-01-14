// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace EventStore.Core.Services.Archive.Naming;

public class ArchiveChunkNameResolver : IArchiveChunkNameResolver {
	private readonly IVersionedFileNamingStrategy _namingStrategy;

	public ArchiveChunkNameResolver(IVersionedFileNamingStrategy namingStrategy) {
		_namingStrategy = namingStrategy;
	}

	public string Prefix => _namingStrategy.Prefix;

	public string ResolveFileName(int logicalChunkNumber) {
		var filePath = _namingStrategy.GetFilenameFor(logicalChunkNumber, version: 1);
		return Path.GetFileName(filePath);
	}

	public int ResolveChunkNumber(ReadOnlySpan<char> fileName) => _namingStrategy.GetIndexFor(fileName);
}
