// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Serilog;

namespace EventStore.Core.Services.Archive.Storage;

public class FileSystemReader : IArchiveStorageReader {
	protected static readonly ILogger Log = Serilog.Log.ForContext<FileSystemReader>();

	private readonly string _archivePath;
	private readonly Func<int?, int?, string> _getChunkPrefix;

	public FileSystemReader(FileSystemOptions options, Func<int?, int?, string> getChunkPrefix) {
		_archivePath = options.Path;
		_getChunkPrefix = getChunkPrefix;
	}

	public IAsyncEnumerable<string> ListChunks(CancellationToken ct) {
		return new DirectoryInfo(_archivePath)
			.EnumerateFiles($"{_getChunkPrefix(null, null)}*")
			.Select(chunk => chunk.Name)
			.Order()
			.ToAsyncEnumerable();
	}
}
